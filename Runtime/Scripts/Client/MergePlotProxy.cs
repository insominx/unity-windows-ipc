using UnityEngine;
using UnityEngine.UI;
using System.Diagnostics;
using System.IO;
using Debug = UnityEngine.Debug;

#if UNITY_EDITOR
using UnityEditor; // Editor file picker
#endif

public class MergePlotProxy : MonoBehaviour
{
    [Header("References")]
    public NamedPipeClientIPC pipe;
    public Button sendDataButton;
    public Button showWindowButton;
    public Button hideWindowButton;

    // Handled by custom editor
    public bool launchProcess;
    public string processToLaunch;

    [Header("Debug")]
    public bool logHeartbeats;
    public bool logReceivedData;

    //---------------------------------------------------------------------------
    void Start()
    {
        sendDataButton.onClick.AddListener(SendSampleData);
        showWindowButton.onClick.AddListener(SendShowWindowCommand);
        hideWindowButton.onClick.AddListener(SendHideWindowCommand);

        NamedPipeClientIPC.OnDataReceived += OnMessageReceived;
        if (!launchProcess || string.IsNullOrWhiteSpace(processToLaunch)) return;
        StartProcess(processToLaunch);
    }

    //---------------------------------------------------------------------------
    void OnDestroy()
    {
        sendDataButton.onClick.RemoveListener(SendSampleData);
        showWindowButton.onClick.RemoveListener(SendShowWindowCommand);
        hideWindowButton.onClick.RemoveListener(SendHideWindowCommand);
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
        catch {}
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
    void SendSampleData()
    {
        string customJSON = JsonUtility.ToJson(new MessageIPC { type = "custom", value = "true" });
        pipe?.Send(customJSON);
    }

    //---------------------------------------------------------------------------
    void SendShowWindowCommand()
    {
        string showJSON = JsonUtility.ToJson(new MessageIPC { type = "show-window", value = "true" });
        Debug.Log($"Sending: {showJSON}");
        pipe?.Send(showJSON);
    }

    //---------------------------------------------------------------------------
    void SendHideWindowCommand()
    {
        string showJSON = JsonUtility.ToJson(new MessageIPC { type = "show-window", value = "false" });
        Debug.Log($"Sending: {showJSON}");
        pipe?.Send(showJSON);
    }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    //---------------------------------------------------------------------------
    static void StartProcess(string exePath, string arguments = "")
    {
        if (string.IsNullOrWhiteSpace(exePath)) return;

        string dir = Path.GetDirectoryName(exePath);
        if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir)) return;
        if (!File.Exists(exePath))
        {
            Debug.LogError($"Cannot find: {exePath}");
            return;
        }

        var info = new ProcessStartInfo
        {
            FileName         = exePath,
            Arguments        = arguments,
            WorkingDirectory = dir,
            UseShellExecute  = false,
            CreateNoWindow   = false
        };

        Process.Start(info);
    }
#endif
}
