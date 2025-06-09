#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN || UNITY_WSA
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Named‑pipe **server** – waits for a client; when it disconnects we loop & wait again.
/// </summary>
public sealed class NamedPipeServerIPC : NamedPipeIPCBase<NamedPipeServerIPC>
{
    protected override async Task<PipeStream> ConnectAsync(CancellationToken tok)
    {
        var server = new NamedPipeServerStream(
            pipeName,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Message,
            PipeOptions.Asynchronous);

        LogVerbose("[Server] Waiting for client connection…");
        await server.WaitForConnectionAsync(tok);
        server.ReadMode = PipeTransmissionMode.Message;
        LogVerbose("[Server] Client connected.");
        return server;
    }
}
#endif
