using System.Collections;
using UnityEngine;
using UnityEngine.UI;
public class HMProxy : MonoBehaviour
{
    [Header("References")]
    public Button hideWindowButton;

    [Header("Config")]
    public bool logHeartbeats;
    public bool logReceivedData;

    [Header("Debug")]
    WindowController _windowCtrl;

    //---------------------------------------------------------------------------
    void Awake()
    {
        _windowCtrl = new WindowController();
        hideWindowButton.onClick.AddListener(OnHideWindowClicked);

        NamedPipeServerIPC.OnDataReceived += NamedPipeServerIPCOnOnDataReceived;
    }

    //---------------------------------------------------------------------------
    void OnDestroy()
    {
        hideWindowButton.onClick.RemoveListener(OnHideWindowClicked);
        NamedPipeServerIPC.OnDataReceived -= NamedPipeServerIPCOnOnDataReceived;
    }

    //---------------------------------------------------------------------------
    void NamedPipeServerIPCOnOnDataReceived(string newData)
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

    IEnumerator HideThenShow()
    {
    #if UNITY_EDITOR
        Debug.LogWarning("No-op in Editor.");
    #endif

        // Debug.Log("[HMProxy] Hiding…");
        _windowCtrl.Hide();

        yield return new WaitForSecondsRealtime(1f);

        // Debug.Log("[HMProxy] Showing…");
        _windowCtrl.Show();
    }

    //---------------------------------------------------------------------------
    void Hide()
    {
    #if UNITY_EDITOR
        Debug.LogWarning("No-op in Editor.");
    #endif

        // Debug.Log("[HMProxy] Hiding…");
        _windowCtrl.Hide();
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
                    _windowCtrl.Show();
            }
        }

        if (logReceivedData)
            Debug.Log(JsonUtility.ToJson(msg));
    }
}
