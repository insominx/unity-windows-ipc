using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Named‑pipe **client** that keeps retrying every second until a server appears.
/// </summary>
public sealed class NamedPipeClientIPC : NamedPipeIPCBase<NamedPipeClientIPC>
{
    [UnityEngine.Header("Client Settings")]
    [UnityEngine.SerializeField] int connectTimeoutMs = 1000;

    protected override async Task<PipeStream> ConnectAsync(CancellationToken tok)
    {
        // Loop internally so we don't throw TimeoutException to the base class.
        while (!tok.IsCancellationRequested)
        {
            var pipe = new NamedPipeClientStream(
                ".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);

            try
            {
                await pipe.ConnectAsync(connectTimeoutMs, tok);
                pipe.ReadMode = PipeTransmissionMode.Message;
                LogVerbose("[Client] Connected.");
                return pipe;
            }
            catch (TimeoutException)       { LogVerbose("[Client] Connect timed out."); }
            catch (IOException ioEx)       { LogVerbose($"[Client] IO on connect: {ioEx.Message}"); }
            finally
            {
                if (!pipe.IsConnected)      // failed attempt – clean up & retry
                    try { pipe.Dispose(); } catch { }
            }

            await Task.Delay(500, tok);     // brief pause before retry
        }

        // Cancellation requested
        throw new OperationCanceledException(tok);
    }
}
#else
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Named‑pipe **client** – no-op implementation for non-Windows platforms.
/// </summary>
public sealed class NamedPipeClientIPC : NamedPipeIPCBase<NamedPipeClientIPC>
{
    protected override Task ConnectAsync(CancellationToken tok)
    {
        LogVerbose("[Client] Named pipes not supported on this platform.");
        return Task.FromException(new System.PlatformNotSupportedException("Named pipes are only supported on Windows platforms"));
    }
}
#endif
