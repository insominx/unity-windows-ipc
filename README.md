# Windows IPC for Unity

This package provides a simple interface for interprocess communication on Windows through named pipes. It contains ready-made client and server components along with sample scenes that demonstrate how to exchange JSON messages between a Unity application and another Windows process.

**Cross-Platform Note**: While this package is designed for Windows, it now compiles on all Unity-supported platforms with no-op fallbacks for non-Windows systems.

## Features

- Asynchronous named-pipe client and server behaviours.
- Example Unity scenes illustrating bi-directional communication.
- Optional utilities for launching or closing external processes.
- **Cross-platform compilation** - compiles on all platforms with graceful fallbacks.
- Designed for Unity **2022.3** or later.

## Platform Support

| Platform | Named Pipes | Window Control | Process Launching | Status |
|----------|-------------|----------------|-------------------|--------|
| **Windows** | ✅ Full Support | ✅ Full Support | ✅ Full Support | **Primary Platform** |
| **macOS** | ❌ No-op | ❌ No-op | ⚠️ Limited | Compiles with warnings |
| **Linux** | ❌ No-op | ❌ No-op | ⚠️ Limited | Compiles with warnings |
| **Other Platforms** | ❌ No-op | ❌ No-op | ⚠️ Limited | Compiles with warnings |

### Non-Windows Behavior

On non-Windows platforms, the package will:
- **Compile successfully** without errors
- **Log warnings** when IPC operations are attempted
- **Provide no-op implementations** for all Windows-specific functionality
- **Maintain the same API surface** for cross-platform compatibility

This allows Unity projects using this package to be opened and built on any platform, even though the IPC functionality only works on Windows.

## Installation

The package is distributed via the Unity Package Manager. To add it to your project:

1. Open **Window → Package Manager** in Unity.
2. Click the **+** button and choose **Add package from git URL...**
3. Enter the URL to this repository and press **Add**.

The manifest entry will look similar to:

```json
"com.humana-machina.windows-ipc": "https://github.com/yourname/unity-windows-ipc.git"
```

After installation you can import the **Demos** sample from the Package Manager window to explore the example client and server scenes.

## Usage

Attach `NamedPipeClientIPC` or `NamedPipeServerIPC` to a GameObject and use the accompanying bridge scripts (`ClientServerIpcBridge` or `ServerClientIpcBridge`) to send or receive `MessageIPC` structures. The sample scenes show how to launch a Windows executable and communicate with it via JSON payloads.

**Note**: On non-Windows platforms, these components will compile and can be attached to GameObjects, but will log warnings and perform no operations when IPC methods are called.

See the scripts under [`Runtime/Scripts`](Runtime/Scripts) for implementation details.

## Author

Michael Guerrero

## License

This project is licensed under the terms of the [MIT License](LICENSE).
