# Architecture

## Design

The solution uses a clean-architecture dependency direction:

```text
OpenClaw.KinectSatellite (WPF UI / tray / composition root)
       |                         |
       v                         v
Core (contracts, options, use case) <--- Infrastructure (device and network adapters)
                                               |
                         Kinect SDK / ONNX Runtime / HA WebSocket / NAudio
```

`Core` contains no Kinect, ONNX, Home Assistant, WPF, or playback implementation. `SatelliteWorker` owns the voice use case; `SatelliteController` supplies explicit start/stop/status semantics to any presentation layer. The WPF host composes services and handles the window and tray lifetime.

## Modules

| Module | Responsibility |
| --- | --- |
| `KinectService` | Discovers a connected Kinect v1 through the SDK 1.8 assembly, selects adaptive array beamforming, enables AEC/AGC/noise suppression, and yields immutable 16 kHz PCM frames. Runtime loading keeps hardware out of unit tests. |
| `WakeWordCatalog` / installer | Exposes only Alexa and Jarvis, retrieves their official ESPHome manifests/assets on first use, verifies repeat-download SHA-256 integrity, and persists source/license/attribution metadata. |
| `MicroWakeWordService` | Converts 30 ms PCM into a rolling 30×40 log-spectrum feature tensor and evaluates compatible ONNX classifiers. It rejects upstream TFLite files explicitly instead of relabeling them. |
| `ViewAssistClient` | Authenticates to `/api/websocket`, starts an `assist_pipeline/run`, prefixes binary PCM with Home Assistant's handler byte, observes pipeline events, and delegates TTS playback. |
| `AudioPlayer` | Downloads Home Assistant's TTS media to a short-lived file and plays it through the Windows output device with NAudio. |
| Configuration | Strongly typed per-user settings shared through a core interface; the UI validates changes before they become current. |
| `UserSettingsStore` | Persists per-user settings under Local AppData, protects the token with current-user Windows DPAPI, and manages the current-user Run registry entry. |
| WPF shell | Validates input, tests connectivity, applies settings, controls service state, reports errors, and owns the notification-area menu. |

## Runtime sequence

1. Kinect SDK creates a beam pointing toward the active speaker and emits processed PCM.
2. The local wake classifier evaluates rolling features; raw audio does not leave the machine before activation.
3. On activation, the satellite authenticates and begins a Home Assistant Assist pipeline at STT and ending at TTS.
4. PCM is streamed as WebSocket binary frames while event messages describe pipeline progress.
5. The returned TTS URL is downloaded and played, after which local listening resumes.

## Boundaries and trade-offs

- Kinect SDK 1.8 is a legacy x86 dependency. Reflection contains that constraint in one adapter and produces an actionable error when it is absent.
- Official Alexa/Jarvis MicroWakeWord exports currently use TensorFlow Lite Micro. The installer preserves and verifies them, but the v0.1.0 ONNX adapter rejects that format with an actionable error. A native model-specific TFLite adapter is required before those catalog entries can perform inference.
- The current interaction ends when Home Assistant emits `run-end`; future endpointing controls will allow explicit audio termination.
- Each activation opens a new authenticated WebSocket. Connection pooling is deferred until reconnection behavior is fully tested.

## Testing strategy

Unit tests substitute contract implementations and verify orchestration. Feature extraction, protocol parsing, retry behavior, and playback cancellation are planned next. Hardware-in-the-loop tests require a Windows x86 runner with SDK 1.8 and are deliberately separate from portable unit tests.
