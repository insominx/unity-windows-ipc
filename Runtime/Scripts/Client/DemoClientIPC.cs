using System;
using UnityEngine;
using UnityEngine.UI;
using System.Diagnostics;
using System.IO;
using Debug = UnityEngine.Debug;

#if UNITY_EDITOR
using UnityEditor;                       // Editor file picker
#endif

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using System.Windows.Forms;              // Runtime file picker (Mono backend)
#endif

public class DemoClientIPC : MonoBehaviour
{
    [Header("References")]
    public NamedPipeClientIPC pipe;
    public Button sendDataButton;
    public Button showWindowButton;
    public Button hideWindowButton;

    [Header("Config")]
    public bool launchProcess;
    public string processToLaunch;

    void Start()
    {
        sendDataButton.onClick.AddListener(SendSampleData);
        showWindowButton.onClick.AddListener(SendShowWindowCommand);
        hideWindowButton.onClick.AddListener(SendHideWindowCommand);

        if (!launchProcess || string.IsNullOrWhiteSpace(processToLaunch)) return;
        StartProcess(processToLaunch);
    }

    void OnDestroy()
    {
        sendDataButton.onClick.RemoveListener(SendSampleData);
        showWindowButton.onClick.RemoveListener(SendShowWindowCommand);
        hideWindowButton.onClick.RemoveListener(SendHideWindowCommand);
    }

    void OnValidate()
    {
        if (!pipe)
            Debug.LogWarning("No pipe assigned");
    }

    void SendSampleData()
    {
        string customJSON = JsonUtility.ToJson(new MessageIPC { type = "custom", value = "true" });
        pipe?.Send(customJSON);
    }

    void SendShowWindowCommand()
    {
        string showJSON = JsonUtility.ToJson(new MessageIPC { type = "show-window", value = "true" });
        Debug.Log($"Sending: {showJSON}");
        pipe?.Send(showJSON);
    }

    void SendHideWindowCommand()
    {
        string showJSON = JsonUtility.ToJson(new MessageIPC { type = "show-window", value = "false" });
        Debug.Log($"Sending: {showJSON}");
        pipe?.Send(showJSON);
    }

    void BrowseForExe()
    {
        string path = string.Empty;

#if UNITY_EDITOR
        path = EditorUtility.OpenFilePanel("Select executable", "", "exe");
#elif UNITY_STANDALONE_WIN
        using (var dlg = new OpenFileDialog { Filter = "Executable (*.exe)|*.exe" })
            if (dlg.ShowDialog() == DialogResult.OK)
                path = dlg.FileName;
#endif

        if (string.IsNullOrWhiteSpace(path)) return;

        processToLaunch = path;
        StartProcess(path);
    }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
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
