using System.Collections;
using UnityEngine;
using UnityEngine.UI;
public class ServerClientIpcBridge : MonoBehaviour
{
    [Header("References")]
    public Button hideWindowButton;

    [Header("Config")]
    WindowController windowController;

    [Header("Debug")]
    public bool logHeartbeats;
    public bool logReceivedData;

    //---------------------------------------------------------------------------
    void Awake()
    {
        windowController = new WindowController();
        hideWindowButton.onClick.AddListener(OnHideWindowClicked);
        NamedPipeServerIPC.OnDataReceived += OnMessageReceived;
        NamedPipeServerIPC.OnConnected    += OnPipeConnected;
    }

    //---------------------------------------------------------------------------
    void OnDestroy()
    {
        hideWindowButton.onClick.RemoveListener(OnHideWindowClicked);
        NamedPipeServerIPC.OnDataReceived -= OnMessageReceived;
        NamedPipeServerIPC.OnConnected    -= OnPipeConnected;
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
    void OnHideWindowClicked()
    {
        StartCoroutine(HideThenShow());
    }

    //---------------------------------------------------------------------------
    IEnumerator HideThenShow()
    {
#if UNITY_EDITOR
        Debug.LogWarning("No-op in Editor.");
#elif !UNITY_STANDALONE_WIN && !UNITY_EDITOR_WIN && !UNITY_WSA
        Debug.LogWarning("Window control is only supported on Windows platforms.");
#endif

        // Debug.Log("[HMProxy] Hiding…");
        windowController.Hide();

        yield return new WaitForSecondsRealtime(1f);

        // Debug.Log("[HMProxy] Showing…");
        windowController.Show();
    }

    //---------------------------------------------------------------------------
    void Hide()
    {
#if UNITY_EDITOR
        Debug.LogWarning("No-op in Editor.");
#elif !UNITY_STANDALONE_WIN && !UNITY_EDITOR_WIN && !UNITY_WSA
        Debug.LogWarning("Window control is only supported on Windows platforms.");
#endif

        // Debug.Log("[HMProxy] Hiding…");
        windowController.Hide();
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

        if (msg.type == "show-window")
        {
            if (bool.TryParse(msg.value, out bool visible))
            {
                if (!visible)
                    Hide();
                else
                    windowController.Show();
            }
        }

        if (logReceivedData)
            Debug.Log(JsonUtility.ToJson(msg));
    }

    //--------------------------------------------------------------------------
    void OnPipeConnected()
    {
        Debug.Log("[Server] Pipe connected.");
    }
}
