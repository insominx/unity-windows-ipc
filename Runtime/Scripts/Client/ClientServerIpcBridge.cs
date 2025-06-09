using UnityEngine;
using Debug = UnityEngine.Debug;

#if UNITY_EDITOR
using UnityEditor; // Editor file picker
#endif

public class ClientServerIpcBridge : MonoBehaviour
{
    [Header("References")]
    public NamedPipeClientIPC pipe;

    // Handled by custom editor
    public bool launchProcess;
    public string processToLaunch;

    [Header("Debug")]
    public bool logHeartbeats;
    public bool logReceivedData;

    readonly ProcessController processController = new();

    //---------------------------------------------------------------------------
    void Start()
    {
        NamedPipeClientIPC.OnDataReceived += OnMessageReceived;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        if (launchProcess && !string.IsNullOrWhiteSpace(processToLaunch))
            processController.Launch(processToLaunch);
#endif
    }

    //---------------------------------------------------------------------------
    void OnDestroy()
    {
        NamedPipeClientIPC.OnDataReceived -= OnMessageReceived;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        processController.Stop();
#endif
    }

    //---------------------------------------------------------------------------
    void OnValidate()
    {
        if (!pipe)
            Debug.LogWarning("No pipe assigned");
    }

    //---------------------------------------------------------------------------
    void OnMessageReceived(string newData)
    {
        try
        {
            var msg = JsonUtility.FromJson<MessageIPC>(newData);
            if (msg != null)
                ProcessMessage(msg);
        }
        catch { }
    }

    //---------------------------------------------------------------------------
    void ProcessMessage(MessageIPC msg)
    {
        if (msg == null) return;

        if (msg.type == "heartbeat")
        {
            if (logHeartbeats)
                Debug.Log(JsonUtility.ToJson(msg));
            return;
        }

        if (logReceivedData)
            Debug.Log(JsonUtility.ToJson(msg));
    }

    //---------------------------------------------------------------------------
    public void SendData(MessageIPC msg)
    {
        pipe?.Send(JsonUtility.ToJson(msg));
    }

    //---------------------------------------------------------------------------
    public void SendSampleData()
    {
        pipe?.Send(JsonUtility.ToJson(new MessageIPC { type = "custom", value = "true" }));
    }

    //---------------------------------------------------------------------------
    public void SendShowWindowCommand()
    {
        pipe?.Send(JsonUtility.ToJson(new MessageIPC { type = "show-window", value = "true" }));
    }

    //---------------------------------------------------------------------------
    public void SendHideWindowCommand()
    {
        pipe?.Send(JsonUtility.ToJson(new MessageIPC { type = "show-window", value = "false" }));
    }

    //---------------------------------------------------------------------------
    /// <summary>Launches an executable via the ProcessController.
    /// This is only intended to be used for testing</summary>
    public void LaunchProcess(string exePath)
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        processController.Launch(exePath);
#else
        Debug.LogWarning("Process launch is only supported on Windows.");
#endif
    }

    //---------------------------------------------------------------------------
    /// <summary>Stops the process launched by this bridge, if any.</summary>
    public void StopProcess()
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        processController.Stop();
#else
        Debug.LogWarning("Process control is only supported on Windows.");
#endif
    }
}
