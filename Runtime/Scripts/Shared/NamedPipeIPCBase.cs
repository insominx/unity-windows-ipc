using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN || UNITY_WSA
using System.IO.Pipes;
#endif
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>
/// Common machinery for both client & server named‑pipe endpoints.
/// Keeps retrying on *expected* failures (timeout, pipe closed) without
/// throwing red errors.  All logs are marshalled to Unity's main thread.
/// </summary>
public abstract class NamedPipeIPCBase<T> : MonoBehaviour where T : NamedPipeIPCBase<T>
{
    // ──────────────────────────────────────────────────────────────────────────
    public delegate void DataReceived(string json);
    public static event DataReceived OnDataReceived;

    public delegate void ConnectionEvent();
    public static event ConnectionEvent OnConnected;

    protected const int  MaxPayloadBytes = 4096;
    protected readonly ConcurrentQueue<string> sendQueue = new();

    [Header("Pipe Settings")]
    [SerializeField] protected string pipeName      = "UnityPipe";
    [SerializeField] protected float  heartbeatSecs = 1f;
    [SerializeField] protected bool   verbose;                  // toggle chatty logs

    CancellationTokenSource _cts;
    Task _loopTask;
    int  _shutdownFlag;

    SynchronizationContext _unityCtx;
    static readonly Stopwatch _timer = Stopwatch.StartNew();

    // ───────────────── Unity lifecycle ─────────────────
    protected virtual void Awake() => _unityCtx = SynchronizationContext.Current;

    protected virtual void Start()
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN || UNITY_WSA
        Log($"[{typeof(T).Name}] Start()");
        _cts      = new CancellationTokenSource();
        _loopTask = Task.Run(() => RunAsync(_cts.Token));
#else
        Log($"[{typeof(T).Name}] Start() - Named pipes not supported on this platform");
#endif
    }

    void OnApplicationQuit()   => Shutdown();
    protected virtual void OnDestroy() => Shutdown();

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN || UNITY_WSA
    // ───────────────── Template method (Windows) ─────────────────
    /// <remarks>
    ///  Must return a *connected* <see cref="PipeStream"/>.
    ///  Throw <see cref="OperationCanceledException"/> to propagate cancellation.
    /// </remarks>
    protected abstract Task<PipeStream> ConnectAsync(CancellationToken tok);
#else
    // ───────────────── Template method (Non-Windows) ─────────────────
    /// <remarks>
    ///  No-op implementation for non-Windows platforms.
    /// </remarks>
    protected virtual Task ConnectAsync(CancellationToken tok)
    {
        return Task.FromException(new PlatformNotSupportedException("Named pipes are only supported on Windows platforms"));
    }
#endif

    // ───────────────── Public API ─────────────────
    public bool Send(string json) => EnqueueMessage(json);

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN || UNITY_WSA
    // ───────────────── Core loop (Windows) ─────────────────
    async Task RunAsync(CancellationToken tok)
    {
        Log($"[{typeof(T).Name}] RunAsync loop started.");
        while (!tok.IsCancellationRequested)
        {
            try
            {
                LogVerbose($"[{typeof(T).Name}] Attempting pipe connect…");
                await using PipeStream pipe = await ConnectAsync(tok).ConfigureAwait(false);
                Log($"[{typeof(T).Name}] Pipe connected.");

                await HandleConnectionAsync(pipe, tok).ConfigureAwait(false);
            }
            catch (OperationCanceledException) {                     // Shutdown / domain reload
                LogVerbose("RunAsync cancelled.");
                break;
            }
            catch (TimeoutException) {                               // Expected – server absent
                LogVerbose("Connect timed out – retrying…");
            }
            catch (IOException ioEx) {                               // Expected when other side leaves
                LogVerbose($"Pipe I/O closed: {ioEx.Message} – retrying…");
            }
            catch (Exception ex) {                                   // Anything else is real trouble
                LogError($"RunAsync unexpected error: {ex}");
            }

            try { await Task.Delay(500, tok).ConfigureAwait(false); } // simple back‑off
            catch (OperationCanceledException) { break; }
        }
        Log($"[{typeof(T).Name}] RunAsync loop exited.");
    }

    async Task HandleConnectionAsync(PipeStream pipe, CancellationToken tok)
    {
        using var reg = tok.Register(() => { try { pipe.Dispose(); } catch { } });

        RaiseConnected();

        var reader = ReaderLoopAsync(pipe, tok);
        var writer = WriterLoopAsync(pipe, tok);

        await Task.WhenAny(reader, writer).ConfigureAwait(false);
        try { await Task.WhenAll(reader, writer).ConfigureAwait(false); } catch { }

        // Flush orphaned messages so the next connection starts clean
        while (sendQueue.TryDequeue(out _)) { }

        LogVerbose($"[{typeof(T).Name}] Pipe disconnected, will retry.");
    }

    // ───────────────── Reader & Writer loops (Windows) ─────────────────
    async Task ReaderLoopAsync(PipeStream pipe, CancellationToken tok)
    {
        const int CHUNK = 8192;
        byte[] buf = new byte[CHUNK];

        try
        {
            while (!tok.IsCancellationRequested && pipe.IsConnected)
            {
                int total = 0;
                do
                {
                    int n = await pipe.ReadAsync(buf, total, buf.Length - total, tok)
                                      .ConfigureAwait(false);
                    if (n == 0) return;                           // remote closed
                    total += n;
                }
                while (!pipe.IsMessageComplete);

                string payload = Encoding.UTF8.GetString(buf, 0, total);
                LogVerbose($"Reader got {total} B: {payload}");
                RaiseDataReceived(payload);
            }
        }
        catch (OperationCanceledException) { }
        catch (IOException)                { }                    // normal on disconnect
        catch (Exception ex)               { LogError($"ReaderLoop error: {ex}"); }
    }

    async Task WriterLoopAsync(PipeStream pipe, CancellationToken tok)
    {
        await using var writer =
            new StreamWriter(pipe, Encoding.UTF8, 1024, leaveOpen: true) { AutoFlush = true };

        double nextBeat = _timer.Elapsed.TotalSeconds + heartbeatSecs;
        var    hb       = new MessageIPC { type = "heartbeat" };

        try
        {
            while (!tok.IsCancellationRequested && pipe.IsConnected)
            {
                while (sendQueue.TryDequeue(out string msg))
                {
                    await writer.WriteLineAsync(msg).ConfigureAwait(false);
                    LogVerbose($"Writer sent user msg: {msg}");
                }

                if (_timer.Elapsed.TotalSeconds >= nextBeat)
                {
                    hb.value = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    await writer.WriteLineAsync(JsonUtility.ToJson(hb)).ConfigureAwait(false);
                    LogVerbose("Writer sent heartbeat.");
                    nextBeat += heartbeatSecs;
                }

                try { await Task.Delay(50, tok).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }
            }
        }
        catch (OperationCanceledException) { }
        catch (IOException)                { }                    // normal on disconnect
        catch (Exception ex)               { LogError($"WriterLoop error: {ex}"); }
    }
#endif

    // ───────────────── Queue & logging helpers ─────────────────
    protected bool EnqueueMessage(string msg)
    {
        if (string.IsNullOrEmpty(msg)) return true;

        int bytes = Encoding.UTF8.GetByteCount(msg);
        if (bytes > MaxPayloadBytes)
        {
            LogError($"Payload {bytes} B exceeds 4 KB limit – rejected.");
            return false;
        }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN || UNITY_WSA
        sendQueue.Enqueue(msg);
        LogVerbose($"Enqueued message: {msg}");
        return true;
#else
        LogVerbose($"Message not sent - Named pipes not supported on this platform: {msg}");
        return false;
#endif
    }

    void RaiseDataReceived(string data)
    {
        if (OnDataReceived == null) return;

        void Invoke() { try { OnDataReceived?.Invoke(data); } catch (Exception ex) { Debug.LogException(ex); } }

        if (_unityCtx != null) _unityCtx.Post(_ => Invoke(), null); else Invoke();
    }

    void RaiseConnected()
    {
        if (OnConnected == null) return;

        void Invoke() { try { OnConnected?.Invoke(); } catch (Exception ex) { Debug.LogException(ex); } }

        if (_unityCtx != null) _unityCtx.Post(_ => Invoke(), null); else Invoke();
    }

    // ——— thread‑safe log wrappers ———
    protected void LogVerbose(string m) { if (verbose) PostLog(m, false); }
    protected void Log       (string m) { PostLog(m, false);             }
    protected void LogError  (string m) { PostLog(m, true );             }

    void PostLog(string msg, bool isErr)
    {
        void Act(object _) { if (isErr) Debug.LogError(msg); else Debug.Log(msg); }
        if (_unityCtx != null) _unityCtx.Post(Act, null); else Act(null);
    }

    // ───────────────── Shutdown ─────────────────
    void Shutdown()
    {
        if (Interlocked.Exchange(ref _shutdownFlag, 1) != 0) return;
        Log($"[{typeof(T).Name}] Shutdown()");

        var cts  = _cts;
        var loop = _loopTask;
        _cts = null;
        _loopTask = null;

        try { cts?.Cancel(); } catch { }

        if (loop != null)
            _ = loop.ContinueWith(_ => { try { cts?.Dispose(); } catch { } },
                                  TaskScheduler.Default);
        else
            cts?.Dispose();
    }
}
