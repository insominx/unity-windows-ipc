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
    [DllImport("user32.dll")] static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr lpdwProcessId);
    [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] static extern bool BringWindowToTop(IntPtr hWnd);

    public IntPtr WindowHandle { get; }

    public WindowController(IntPtr? forcedHwnd = null)
    {
        if (IsInEditor()) return;
        WindowHandle = forcedHwnd ?? Process.GetCurrentProcess().MainWindowHandle;
        if (WindowHandle == IntPtr.Zero)
            Debug.LogError("[WindowController] Failed to get window handle!");
    }

    bool ValidHandle => WindowHandle != IntPtr.Zero && IsWindow(WindowHandle);

    public void Hide()
    {
        if (IsInEditor() || !ValidHandle) return;
        ShowWindow(WindowHandle, SW_HIDE);
    }

    public void Show(bool restore = true)
    {
        if (IsInEditor() || !ValidHandle) return;

        // 1) Restore if minimized
        if (restore)
            ShowWindow(WindowHandle, SW_RESTORE);

        // 2) Attach input threads so SetForegroundWindow can succeed
        IntPtr fg = GetForegroundWindow();
        uint fgThread = GetWindowThreadProcessId(fg, IntPtr.Zero);
        uint myThread = GetCurrentThreadId();
        AttachThreadInput(myThread, fgThread, true);

        // 3) Bring to top
        BringWindowToTop(WindowHandle);

        // 4) Finally set foreground
        SetForegroundWindow(WindowHandle);

        // Detach
        AttachThreadInput(myThread, fgThread, false);
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
