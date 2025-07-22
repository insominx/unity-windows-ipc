/*
 * ProcessController.cs
 * Encapsulates process launch and termination logic
 */
using System;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using Debug = UnityEngine.Debug;

public class ProcessController
{
    private Process _process;

    // Launches an external executable.
    public void Launch(string exePath, string arguments = "")
    {
        if (string.IsNullOrWhiteSpace(exePath))
        {
            Debug.LogError("ProcessController: empty executable path.");
            return;
        }

#if !UNITY_STANDALONE_WIN && !UNITY_EDITOR_WIN && !UNITY_WSA
        // Warn on non-Windows platforms since this is typically used for Windows executables
        if (exePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            Debug.LogWarning("ProcessController: Attempting to launch .exe file on non-Windows platform. This will likely fail.");
        }
        Debug.LogWarning("ProcessController: Process launching is primarily designed for Windows. Cross-platform compatibility may vary.");
#endif

        string dir = Path.GetDirectoryName(exePath);
        if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir) || !File.Exists(exePath))
        {
            Debug.LogError($"ProcessController: cannot find executable: {exePath}");
            return;
        }

        var psi = new ProcessStartInfo
        {
            FileName = exePath,
            Arguments = arguments,
            WorkingDirectory = dir,
            UseShellExecute = false,
            CreateNoWindow = false
        };

        try
        {
            _process = Process.Start(psi);
            if (_process != null)
                Debug.Log($"ProcessController: Launched [{_process.Id}] {Path.GetFileName(exePath)}");
            else
                Debug.LogWarning("ProcessController: Process.Start returned null.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"ProcessController: Failed to launch process: {ex.Message}");
#if !UNITY_STANDALONE_WIN && !UNITY_EDITOR_WIN && !UNITY_WSA
            Debug.LogError("ProcessController: Process launching may not be supported on this platform or the executable format may be incompatible.");
#endif
        }
    }

    /// <summary>
    /// Attempts graceful exit, then kills if still running.
    /// </summary>
    public void Stop()
    {
        if (_process == null)
            return;

        try
        {
            if (!_process.HasExited)
            {
                // Ask politely
                if (_process.CloseMainWindow())
                {
                    if (!_process.WaitForExit(2000))
                        Debug.LogWarning($"ProcessController: timeout waiting for graceful exit of [{_process.Id}].");
                }

                // Force kill if still alive
                if (!_process.HasExited)
                {
                    Debug.LogWarning($"ProcessController: Killing [{_process.Id}].");
                    _process.Kill();
                    _process.WaitForExit(1000);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"ProcessController: Error while stopping process: {ex.Message}");
        }
        finally
        {
            try { _process.Dispose(); } catch { }
            _process = null;
        }
    }

    /// <summary>
    /// Returns true if a process is currently running.
    /// </summary>
    public bool IsRunning => _process != null && !_process.HasExited;
}