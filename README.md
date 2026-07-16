# OpenClaw Kinect Satellite

A user-friendly Windows .NET 8 desktop satellite that gives a Kinect for Windows (v1) a second life as a far-field microphone for [Home Assistant Assist](https://www.home-assistant.io/voice_control/). Its native WPF settings window and system-tray menu control the background voice service.

> **Project status:** developer preview. The Kinect SDK v1 is legacy 32-bit software, so the host intentionally publishes as `win-x86`.

## Prerequisites

- Windows 10/11 x64, .NET 8 SDK (the app itself runs as x86)
- Kinect for Windows sensor and **Kinect for Windows SDK 1.8** (not Kinect v2)
- Home Assistant with an Assist pipeline and a long-lived access token
- Internet access on first use to retrieve the selected official Alexa or Hey Jarvis model and its license metadata

## Build and run for development

1. Install the prerequisites and clone the repository.
2. Build and launch from a **Developer PowerShell for Visual Studio**:

   ```powershell
   dotnet restore
   dotnet build
   dotnet run --project src/OpenClaw.KinectSatellite
   ```

3. In the settings window, enter the Home Assistant URL and long-lived token, choose **Alexa** or **Jarvis**, and adjust wake-word/Kinect options. No model path is required.
4. Select **Test connection**, then **Apply and save**, then **Start**. Closing the window keeps the app in the system tray; use its menu to open settings, start/stop, or exit.

## Download

For versioned builds, open the repository's **Releases** page, download `OpenClaw-Kinect-Satellite-win-x86.zip`, and extract the complete archive before running `OpenClaw.KinectSatellite.exe`. The package is self-contained, so the .NET runtime does not need to be installed; Kinect for Windows SDK v1.8 is still required.

Every pull request and manually dispatched Windows build also exposes the ZIP as the `OpenClaw-Kinect-Satellite-win-x86` workflow artifact. GitHub sign-in is normally required to download workflow artifacts.

Publish a self-contained Windows executable with:

```powershell
dotnet publish src/OpenClaw.KinectSatellite -p:PublishProfile=win-x86-self-contained
```

The output is `src\OpenClaw.KinectSatellite\bin\Release\net8.0-windows\win-x86\publish\OpenClaw.KinectSatellite.exe`. Copy the publish directory to a per-user location such as `%LOCALAPPDATA%\Programs\OpenClaw Kinect Satellite`, then run the executable. No .NET runtime installation is required for this self-contained build. Kinect SDK 1.8 must still be installed.

Maintainers can create the `v0.1.0` GitHub Release either by pushing the `v0.1.0` tag or by running **Windows build and publish** from the Actions page with **Create a GitHub Release** enabled and version `v0.1.0`. The workflow tests, publishes, packages, uploads the artifact, and attaches the same ZIP to the release.

The Kinect SDK performs adaptive beamforming, automatic gain control, acoustic echo cancellation, and noise suppression before 16 kHz mono PCM reaches the wake-word detector. Logs are written beneath `%LOCALAPPDATA%\OpenClaw\KinectSatellite\logs`.

### Wake-word assets and integrity

Windows uses the official **openWakeWord v0.5.1 ONNX pipeline**: the pinned mel-spectrogram model, shared embedding model, and the selected Alexa or Hey Jarvis classifier. The app processes 16 kHz PCM in 80 ms chunks and runs the same streaming feature stages expected by those classifiers. It does not rename ESPHome MicroWakeWord TensorFlow Lite files or feed an incompatible hand-written tensor to an ONNX classifier.

On first start, the pinned ONNX assets are downloaded twice and their SHA-256 digests must match. The digest, exact source URL, license, and attribution are cached under `%LOCALAPPDATA%\OpenClaw\KinectSatellite\models\openwakeword-v0.5.1`; every later load rechecks all model files. An internet connection is required for the first start of each wake word.

openWakeWord code is Apache-2.0. Its included pre-trained Alexa and Hey Jarvis models are **CC BY-NC-SA 4.0**, so attribution, ShareAlike and non-commercial-use restrictions apply. See [`models/THIRD-PARTY-NOTICES.md`](models/THIRD-PARTY-NOTICES.md).

## Kinect SDK detection

The .NET 8 host loads `Microsoft.Kinect.dll` from the executable directory, `KINECTSDK10_DIR`, the standard SDK v1.8 installation folders, or the .NET Framework GAC. If **Start** still fails, copy the complete diagnostic message from the status area; it lists every searched path and the process architecture. The published application must remain win-x86.

## Development

```powershell
dotnet restore
dotnet build --no-restore
dotnet test --no-build
```

Hardware and Home Assistant are isolated behind interfaces, so orchestration can be tested without either. See [architecture](docs/ARCHITECTURE.md), [configuration reference](docs/CONFIGURATION.md), and [roadmap](docs/ROADMAP.md).

## Security

Use HTTPS/WSS when Home Assistant is not on a trusted network. The token is never written to `appsettings.json` or the ordinary settings JSON: it is encrypted with Windows DPAPI for the current Windows user and stored under `%LOCALAPPDATA%\OpenClaw\KinectSatellite`. It cannot be decrypted by another user account. Do not copy `token.dat` between accounts or machines.
