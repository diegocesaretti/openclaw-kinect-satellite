# OpenClaw Kinect Satellite

A user-friendly Windows .NET 8 desktop satellite that gives a Kinect for Windows (v1) a second life as a far-field microphone for [Home Assistant Assist](https://www.home-assistant.io/voice_control/). Its native WPF settings window and system-tray menu control the background voice service.

> **Project status:** developer preview. The Kinect SDK v1 is legacy 32-bit software, so the host intentionally publishes as `win-x86`.

## Prerequisites

- Windows 10/11 x64, .NET 8 SDK (the app itself runs as x86)
- Kinect for Windows sensor and **Kinect for Windows SDK 1.8** (not Kinect v2)
- Home Assistant with an Assist pipeline and a long-lived access token
- A MicroWakeWord-compatible ONNX model accepting `[1, 30, 40]` log-spectrum frames and returning a floating-point wake probability

## Build and run for development

1. Install the prerequisites and clone the repository.
2. Build and launch from a **Developer PowerShell for Visual Studio**:

   ```powershell
   dotnet restore
   dotnet build
   dotnet run --project src/OpenClaw.KinectSatellite
   ```

3. In the settings window, enter the Home Assistant URL and long-lived token, select a compatible `.onnx` model, and adjust wake-word/Kinect options.
4. Select **Test connection**, then **Apply and save**, then **Start**. Closing the window keeps the app in the system tray; use its menu to open settings, start/stop, or exit.

Publish a self-contained Windows executable with:

```powershell
dotnet publish src/OpenClaw.KinectSatellite -p:PublishProfile=win-x86-self-contained
```

The output is `src\OpenClaw.KinectSatellite\bin\Release\net8.0-windows\win-x86\publish\OpenClaw.KinectSatellite.exe`. Copy the publish directory to a per-user location such as `%LOCALAPPDATA%\Programs\OpenClaw Kinect Satellite`, then run the executable. No .NET runtime installation is required for this self-contained build. Kinect SDK 1.8 must still be installed.

The Kinect SDK performs adaptive beamforming, automatic gain control, acoustic echo cancellation, and noise suppression before 16 kHz mono PCM reaches the wake-word detector. Logs are written beneath `%LOCALAPPDATA%\OpenClaw\KinectSatellite\logs`.

## Development

```powershell
dotnet restore
dotnet build --no-restore
dotnet test --no-build
```

Hardware and Home Assistant are isolated behind interfaces, so orchestration can be tested without either. See [architecture](docs/ARCHITECTURE.md), [configuration reference](docs/CONFIGURATION.md), and [roadmap](docs/ROADMAP.md).

## Security

Use HTTPS/WSS when Home Assistant is not on a trusted network. The token is never written to `appsettings.json` or the ordinary settings JSON: it is encrypted with Windows DPAPI for the current Windows user and stored under `%LOCALAPPDATA%\OpenClaw\KinectSatellite`. It cannot be decrypted by another user account. Do not copy `token.dat` between accounts or machines.
