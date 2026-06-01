# Native Runtime Folder

This folder is reserved for app-local vendor/native DLLs used by camera, RTSP, and AI adapters.

The WPF app calls `NativeDependencyLoader.Configure(...)` during startup and registers these folders:

```text
Native\VLAD
Native\VLAD\plugins
Native\IMV\x64
Native\AI\x64
```

Recommended local deployment layout:

```text
AI.Vision.IOInspector.App.exe
DB\DataBase.db
Native\VLAD\VLAD_SDK.dll
Native\VLAD\jsoncpp.dll
Native\VLAD\libvlc.dll
Native\VLAD\libvlccore.dll
Native\VLAD\plugins\...
Native\IMV\x64\MVSDK_Net.dll
Native\IMV\x64\CLIDelegate.dll
Native\IMV\x64\ThridLibray.dll
Native\IMV\x64\MVSDKmd.dll
Native\AI\x64\onnxruntime.dll
Native\AI\x64\opencv_world453.dll
Native\AI\x64\tensorflow.dll
```

Do not commit large vendor DLLs directly to GitHub. Copy them into this folder on the build/deployment PC, or package them as a release artifact/installer payload.
