using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public class NamedPipeServerIPC : NamedPipeIPCBase<NamedPipeServerIPC>
{
    [Header("Named Pipe Settings")]
    [Tooltip("Name of the pipe to create/connect to.")]
    public string pipeName = "UnityPipe";

    [Tooltip("Heartbeat interval in seconds.")]
    public float heartbeatInterval = 1.0f;

    [Header("Config")]
    public bool verbose;

    CancellationTokenSource cancelSource;
    Task pipeTask;
    int shutdownFlag;

    public bool Send(string message)
    {
        return EnqueueMessage(message);
    }

    void Start()
    {
        cancelSource = new CancellationTokenSource();
        pipeTask = Task.Run(() => RunPipeServerAsync(cancelSource.Token));
    }

    void OnApplicationQuit() => Shutdown();
    void OnDestroy() => Shutdown();

    void Shutdown()
    {
        if (Interlocked.Exchange(ref shutdownFlag, 1) != 0) return;
        try { cancelSource?.Cancel(); }
        catch { }
        if (pipeTask != null) _ = pipeTask.ContinueWith(_ => { }, TaskScheduler.Default);
        cancelSource?.Dispose();
        cancelSource = null;
    }

    async Task RunPipeServerAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            using var server = new NamedPipeServerStream(
                pipeName,
                PipeDirection.InOut,
                1,
                PipeTransmissionMode.Message,
                PipeOptions.Asynchronous);

            using var _ = token.Register(() =>
            {
                try { server.Dispose(); }
                catch { }
            });

            Log("Waiting for named pipe client to connect…");
            try { await server.WaitForConnectionAsync(token); }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                LogError("Error waiting for pipe connection: " + ex.Message);
                if (token.IsCancellationRequested) break;
                await Task.Delay(1000, token);
                continue;
            }

            if (!server.IsConnected || token.IsCancellationRequested) break;

            Log("Named pipe client connected.");
            Task readerTask = Task.Run(() => PipeReaderLoop(server, token));
            Task writerTask = Task.Run(() => PipeWriterLoop(server, token));

            await Task.WhenAny(readerTask, writerTask).ConfigureAwait(false);
            try { await Task.WhenAll(readerTask, writerTask).ConfigureAwait(false); }
            catch { }

            Log("Named pipe connection closed.");
        }
        Log("Pipe server loop terminated.");
    }

    async Task PipeReaderLoop(PipeStream pipe, CancellationToken token)
    {
        using var reg = token.Register(() =>
        {
            try { pipe.Dispose(); }
            catch { }
        });
        byte[] buffer = new byte[MaxPayloadBytes];

        try
        {
            while (!token.IsCancellationRequested && pipe.IsConnected)
            {
                int total = 0;
                do
                {
                    int n = await pipe.ReadAsync(buffer, total, buffer.Length - total, token)
                        .ConfigureAwait(false);
                    if (n == 0) return;
                    total += n;
                }
                while (!pipe.IsMessageComplete);

                string payload = Encoding.UTF8.GetString(buffer, 0, total);
                if (!string.IsNullOrWhiteSpace(payload))
                    RaiseDataReceived(payload);
            }
        }
        catch (OperationCanceledException) { }
        catch (IOException ioEx) { Log($"Pipe read ended: {ioEx.Message}"); }
        catch (Exception ex) { LogError("Unexpected error in pipe reader: " + ex); }
    }

    async Task PipeWriterLoop(PipeStream pipe, CancellationToken token)
    {
        // The writer now owns the pipe until the outer "using var server" in RunPipeServerAsync disposes it.
        using var writer = new StreamWriter(pipe, Encoding.UTF8, 1024) { AutoFlush = true };

        DateTime nextHeartbeat = DateTime.UtcNow.AddSeconds(heartbeatInterval);
        MessageIPC heartBeatMsg = new() { type = "heartbeat" };

        try
        {
            while (!token.IsCancellationRequested && pipe.IsConnected)
            {
                // 1) Drain any outgoing messages
                while (sendQueue.TryDequeue(out var msg))
                {
                    await writer.WriteLineAsync(msg).ConfigureAwait(false);
                    if (verbose) Log($"Sent: {msg}");
                }

                // 2) Emit a heartbeat if it’s time
                if (DateTime.UtcNow >= nextHeartbeat)
                {
                    string timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
                    heartBeatMsg.value = timestamp;

                    await writer.WriteLineAsync(JsonUtility.ToJson(heartBeatMsg)).ConfigureAwait(false);
                    nextHeartbeat = DateTime.UtcNow.AddSeconds(heartbeatInterval);
                }

                await Task.Delay(50, token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) { /* Log("PipeWriterLoop canceled by token."); */ }
        catch (IOException ioEx) { Log($"Pipe write ended: {ioEx.Message}"); }
        catch (Exception ex) { LogError($"Unexpected error in pipe writer: {ex}"); }
        // no finally-dispose of 'pipe' here—outer RunPipeServerAsync still has the using-block that will clean it up
    }


    protected override void Log(string msg)
    {
        if (!verbose) return;
        base.Log(msg);
    }

    protected override void LogError(string msg)
    {
        base.LogError(msg);
    }
}
