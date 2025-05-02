using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Debug = UnityEngine.Debug;

public class WindowController
{
    const int SW_HIDE    = 0;
    const int SW_RESTORE = 9;

    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] static extern bool IsWindow(IntPtr hWnd);

    public IntPtr WindowHandle { get; }

    public WindowController(IntPtr? forcedHwnd = null)
    {
        if (IsInEditor()) return;

        // Allow overriding for tests
        WindowHandle = forcedHwnd
            ?? Process.GetCurrentProcess().MainWindowHandle;

        if (WindowHandle == IntPtr.Zero)
            Debug.LogError("[WindowController] Failed to get window handle!");
        // else
        //     Debug.Log($"[WindowController] Got hWnd=0x{WindowHandle.ToInt64():X}");
    }

    bool ValidHandle => WindowHandle != IntPtr.Zero && IsWindow(WindowHandle);

    public void Hide()
    {
        if (IsInEditor() || !ValidHandle) return;
        // Debug.Log($"[WindowController.Hide] hWnd=0x{WindowHandle.ToInt64():X}");
        ShowWindow(WindowHandle, SW_HIDE);
    }

    public void Show(bool restore = true)
    {
        if (IsInEditor() || !ValidHandle) return;
        // Debug.Log($"[WindowController.Show] hWnd=0x{WindowHandle.ToInt64():X}");
        ShowWindow(WindowHandle, restore ? SW_RESTORE : SW_HIDE);
        SetForegroundWindow(WindowHandle);
    }

    bool IsInEditor()
    {
    #if UNITY_EDITOR
        return true;
    #else
        return false;
    #endif
    }
}
