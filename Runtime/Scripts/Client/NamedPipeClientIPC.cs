#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN || UNITY_WSA
using System;
using System.Collections.Concurrent;
using System.Diagnostics;                    // ★ NEW
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>
/// Named-pipe client for Unity that
/// • connects to a server
/// • sends/receives ≤ 4 KB messages + 1 Hz heart-beats
/// • never touches Unity API off the main thread
/// • shuts down cleanly with no leaked tasks
/// </summary>
public sealed class NamedPipeClientIPC : MonoBehaviour
{
    // ─────────────── Inspector values ───────────────
    [Header("Pipe Settings")]
    [SerializeField] string pipeName           = "UnityPipe";
    [SerializeField] int    connectTimeoutMs   = 1000;   // 1 s
    [SerializeField] float  reconnectDelaySecs = 0.5f;   // retry back-off
    [SerializeField] float  heartbeatInterval  = 1.0f;   // seconds

    // ─────────────── Runtime state ───────────────
    readonly ConcurrentQueue<string> sendQueue = new(); // thread-safe
    CancellationTokenSource cancelSrc;
    SynchronizationContext  unityCtx;
    Task                    loopTask;
    int                     shutdownFlag;               // 0 = running, 1 = shutting down
    volatile int            connectedFlag;              // 0 = not connected, 1 = connected

    const int MaxPayloadBytes = 4096;                   // hard limit

    // ─────────────── Unity lifecycle ───────────────
    void Start()
    {
        unityCtx  = SynchronizationContext.Current;
        cancelSrc = new CancellationTokenSource();
        loopTask  = Task.Run(() => ClientLoopAsync(cancelSrc.Token)); // background loop
    }

    void OnApplicationQuit() => Shutdown();
    void OnDestroy()         => Shutdown();

    // ─────────────── Public API ───────────────
    public bool Send(string message)
    {
        if (connectedFlag == 0)                 // gate on live connection
            return false;

        if (string.IsNullOrEmpty(message))
            return true;

        int bytes = Encoding.UTF8.GetByteCount(message);
        if (bytes > MaxPayloadBytes)
        {
            LogErr($"IPC Send rejected – payload {bytes} B exceeds 4 KB limit.");
            return false;
        }

        sendQueue.Enqueue(message);
        return true;
    }

    // ─────────────── Graceful shutdown ───────────────
    void Shutdown()
    {
        if (Interlocked.Exchange(ref shutdownFlag, 1) != 0)
            return;

        var src  = cancelSrc;
        var task = loopTask;
        cancelSrc = null;

        try { src?.Cancel(); } catch { }
        if (task != null) _ = task.ContinueWith(_ => { }, TaskScheduler.Default);
        src?.Dispose();
    }

    // ─────────────── Main client loop ───────────────
    async Task ClientLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            using var client = new NamedPipeClientStream(
                ".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            using var reg = token.Register(() => { try { client.Dispose(); } catch { } });

            try
            {
                await client.ConnectAsync(connectTimeoutMs, token);
                client.ReadMode = PipeTransmissionMode.Message;   // enable message framing
                connectedFlag  = 1;
                Log("Pipe connected.");

                var readTask  = ReaderLoopAsync(client, token);
                var writeTask = WriterLoopAsync(client, token);

                await Task.WhenAny(readTask, writeTask).ConfigureAwait(false);
                try { await Task.WhenAll(readTask, writeTask).ConfigureAwait(false); } catch { }
            }
            catch (TimeoutException)           { }
            catch (OperationCanceledException) { break; }
            catch (IOException ioEx)           { Log($"Pipe I/O closed: {ioEx.Message}"); }
            catch (Exception ex)               { LogErr("Client error: " + ex); }
            finally
            {
                connectedFlag = 0;
                while (sendQueue.TryDequeue(out _)) { }          // flush queue
            }

            if (token.IsCancellationRequested) break;
            try { await Task.Delay(TimeSpan.FromSeconds(reconnectDelaySecs), token).ConfigureAwait(false); }
            catch (OperationCanceledException) { }
        }

        Log("Client loop ended.");
    }

    // ─────────────── Reader (message mode) ───────────────
    async Task ReaderLoopAsync(PipeStream pipe, CancellationToken token)
    {
        using var reg = token.Register(() => { try { pipe.Dispose(); } catch { } });
        byte[] buf = new byte[MaxPayloadBytes];

        try
        {
            while (!token.IsCancellationRequested && pipe.IsConnected)
            {
                int total = 0;
                do
                {
                    int n = await pipe.ReadAsync(buf, total, buf.Length - total, token).ConfigureAwait(false);
                    if (n == 0) return;          // server closed
                    total += n;
                }
                while (!pipe.IsMessageComplete);

                string payload = Encoding.UTF8.GetString(buf, 0, total);
                if (payload.StartsWith("HEARTBEAT"))
                    Log($"Received {payload}");
                else
                    Log($"Received: {payload}");
            }
        }
        catch (OperationCanceledException) { }
        catch (IOException ioEx)           { Log($"Pipe read ended: {ioEx.Message}"); }
        catch (Exception ex)               { LogErr("Reader: " + ex); }
    }

    // ─────────────── Writer (queue + heart-beat) ───────────────
    async Task WriterLoopAsync(PipeStream pipe, CancellationToken token)
    {
        using var reg = token.Register(() => { try { pipe.Dispose(); } catch { } });
        using var writer = new StreamWriter(pipe, Encoding.UTF8, 1024, leaveOpen: true) { AutoFlush = true };

        var stopwatch     = Stopwatch.StartNew();      // ★ thread-safe timer
        double nextBeatAt = heartbeatInterval;         // seconds

        try
        {
            while (!token.IsCancellationRequested && pipe.IsConnected)
            {
                // flush queued messages
                while (sendQueue.TryDequeue(out string msg))
                {
                    await writer.WriteLineAsync(msg).ConfigureAwait(false);
                    Log($"Sent {msg}");
                }

                // heart-beat
                if (stopwatch.Elapsed.TotalSeconds >= nextBeatAt)
                {
                    MessageIPC msg = new() { type = "heartbeat", value = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") };
                    await writer.WriteLineAsync(JsonUtility.ToJson(msg)).ConfigureAwait(false);
                    // Log($"Sent hb");
                    nextBeatAt += heartbeatInterval;
                }

                // gentle throttle (<50 ms latency)
                try { await Task.Delay(50, token).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }
            }
        }
        catch (OperationCanceledException) { }
        catch (IOException)                { }
        catch (Exception ex)               { LogErr("Writer: " + ex); }
    }

    // ─────────────── Thread-safe logging ───────────────
    void Log   (string m) => Post(_ => { if (Application.isPlaying) Debug.Log   (m); });
    void LogErr(string m) => Post(_ => { if (Application.isPlaying) Debug.LogError(m); });

    void Post(SendOrPostCallback cb)
    {
        if (unityCtx != null)
            unityCtx.Post(cb, null);
        else
            cb(null);
    }
}
#endif
