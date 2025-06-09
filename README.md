# Windows IPC for Unity

This package provides a simple interface for interprocess communication on Windows through named pipes. It contains ready-made client and server components along with sample scenes that demonstrate how to exchange JSON messages between a Unity application and another Windows process.

## Features

- Asynchronous named-pipe client and server behaviours.
- Example Unity scenes illustrating bi-directional communication.
- Optional utilities for launching or closing external processes.
- Designed for Unity **2022.3** or later on Windows platforms.

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

See the scripts under [`Runtime/Scripts`](Runtime/Scripts) for implementation details.

## Author

Michael Guerrero

## License

This project is licensed under the terms of the [MIT License](LICENSE).
