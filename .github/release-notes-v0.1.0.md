# OpenClaw Kinect Satellite v0.1.0

This is the first developer-preview release of the OpenClaw Kinect Satellite for Windows.

## Included

- Native .NET 8 WPF settings window and system-tray controls.
- Kinect for Windows SDK v1 microphone-array capture with adaptive beamforming, echo cancellation, and noise suppression.
- Built-in Alexa and Jarvis wake-word catalog with verified official first-run downloads.
- Home Assistant Assist pipeline audio streaming and response playback.
- Per-user settings with a Windows DPAPI-protected Home Assistant access token.
- Self-contained 32-bit Windows package for Kinect SDK v1.8.

## Install

1. Install Kinect for Windows SDK v1.8 and connect a Kinect v1 sensor.
2. Download `OpenClaw-Kinect-Satellite-win-x86.zip` and extract the entire archive to a writable folder.
3. Run `OpenClaw.KinectSatellite.exe`.
4. Select Alexa or Jarvis, configure Home Assistant, test the connection, save, and start the satellite.

> This is a developer preview. Official Alexa/Jarvis artifacts are downloaded with SHA-256 verification and attribution on first use rather than bundled. Their current upstream TensorFlow Lite format is retained unchanged and v0.1.0 reports that it cannot execute that format with its ONNX runtime. Kinect SDK v1.8 remains a separate prerequisite.
