# OpenClaw Kinect Satellite

A Windows .NET 8 voice satellite that gives a Kinect for Windows (v1) a second life as a far-field microphone for [Home Assistant Assist](https://www.home-assistant.io/voice_control/). The service captures the Kinect microphone array's beamformed, echo-cancelled audio, detects a wake word locally with a MicroWakeWord ONNX model, streams speech to Home Assistant, and plays the response.

> **Project status:** developer preview. The Kinect SDK v1 is legacy 32-bit software, so the host intentionally publishes as `win-x86`.

## Prerequisites

- Windows 10/11 x64, .NET 8 SDK (the app itself runs as x86)
- Kinect for Windows sensor and **Kinect for Windows SDK 1.8** (not Kinect v2)
- Home Assistant with an Assist pipeline and a long-lived access token
- A MicroWakeWord-compatible ONNX model accepting `[1, 30, 40]` log-spectrum frames and returning a floating-point wake probability

## Configure and run

1. Clone the repository and put the model at `models/okay-nabu.onnx` (models are not redistributed by this project).
2. Keep secrets out of `appsettings.json`; use an environment variable:

   ```powershell
   $env:HomeAssistant__AccessToken = "your-long-lived-token"
   $env:HomeAssistant__Url = "http://homeassistant.local:8123"
   dotnet run --project src/OpenClaw.KinectSatellite
   ```

3. Adjust `WakeWord:Threshold` for the room and model. Set `HomeAssistant:PipelineId` or leave it `null` for Home Assistant's preferred pipeline.

Publish a self-contained Windows executable with:

```powershell
dotnet publish src/OpenClaw.KinectSatellite -c Release -r win-x86 --self-contained
```

The Kinect SDK performs adaptive beamforming, automatic gain control, acoustic echo cancellation, and noise suppression before 16 kHz mono PCM reaches the wake-word detector. Logs are structured through Serilog and written to the console and daily files under `logs/`.

## Development

```powershell
dotnet restore
dotnet build --no-restore
dotnet test --no-build
```

Hardware and Home Assistant are isolated behind interfaces, so orchestration can be tested without either. See [architecture](docs/ARCHITECTURE.md), [configuration reference](docs/CONFIGURATION.md), and [roadmap](docs/ROADMAP.md).

## Security

Use HTTPS/WSS when Home Assistant is not on a trusted network. Store the access token in an environment variable, Windows Credential Manager, or a service secret—never commit it. The example token in configuration is deliberately invalid.

