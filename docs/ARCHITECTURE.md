# Architecture

## Design

The solution uses a clean-architecture dependency direction:

```text
OpenClaw.KinectSatellite (composition root / Worker host)
       |                         |
       v                         v
Core (contracts, options, use case) <--- Infrastructure (device and network adapters)
                                               |
                         Kinect SDK / ONNX Runtime / HA WebSocket / NAudio
```

`Core` contains no Kinect, ONNX, Home Assistant, or playback implementation. `SatelliteWorker` owns the use case: listen, detect, run one Assist conversation, reset, and resume listening. The host is only a composition root and provides lifetime management, configuration, dependency injection, and Serilog.

## Modules

| Module | Responsibility |
| --- | --- |
| `KinectService` | Discovers a connected Kinect v1 through the SDK 1.8 assembly, selects adaptive array beamforming, enables AEC/AGC/noise suppression, and yields immutable 16 kHz PCM frames. Runtime loading keeps hardware out of unit tests. |
| `MicroWakeWordService` | Converts 30 ms PCM into a rolling 30×40 log-spectrum feature tensor and evaluates an ONNX wake classifier. Consecutive-frame and cooldown gates reduce false activations. |
| `ViewAssistClient` | Authenticates to `/api/websocket`, starts an `assist_pipeline/run`, prefixes binary PCM with Home Assistant's handler byte, observes pipeline events, and delegates TTS playback. |
| `AudioPlayer` | Downloads Home Assistant's TTS media to a short-lived file and plays it through the Windows output device with NAudio. |
| Configuration | Strongly typed, startup-validated options bound by the DI composition module. Environment variables override JSON. |

## Runtime sequence

1. Kinect SDK creates a beam pointing toward the active speaker and emits processed PCM.
2. The local wake classifier evaluates rolling features; raw audio does not leave the machine before activation.
3. On activation, the satellite authenticates and begins a Home Assistant Assist pipeline at STT and ending at TTS.
4. PCM is streamed as WebSocket binary frames while event messages describe pipeline progress.
5. The returned TTS URL is downloaded and played, after which local listening resumes.

## Boundaries and trade-offs

- Kinect SDK 1.8 is a legacy x86 dependency. Reflection contains that constraint in one adapter and produces an actionable error when it is absent.
- MicroWakeWord exports differ. The initial adapter supports a single-input, single-probability ONNX export with a `[1,30,40]` input. Stateful TFLite/ONNX exports require a model-specific adapter.
- The current interaction ends when Home Assistant emits `run-end`; future endpointing controls will allow explicit audio termination.
- Each activation opens a new authenticated WebSocket. Connection pooling is deferred until reconnection behavior is fully tested.

## Testing strategy

Unit tests substitute contract implementations and verify orchestration. Feature extraction, protocol parsing, retry behavior, and playback cancellation are planned next. Hardware-in-the-loop tests require a Windows x86 runner with SDK 1.8 and are deliberately separate from portable unit tests.

