# Configuration reference

Configuration is edited through the built-in WPF window. **Apply and save** validates every field and, when the satellite is running, restarts it so the new audio/model settings take effect.

| Setting | Default | Validation and behavior |
| --- | --- | --- |
| Home Assistant URL | `http://homeassistant.local:8123` | Absolute HTTP(S) URL. **Test connection** calls the authenticated `/api/` endpoint and shows the exact result. |
| Access token | empty | Required. Stored separately using current-user Windows DPAPI, never in source-controlled or plain-text settings. |
| Pipeline/device ID | empty | Optional identifiers passed to `assist_pipeline/run`. |
| Wake word | `Alexa` | Select `Alexa` or `Jarvis`; the official manifest/model and license are retrieved automatically on first start. There is no editable model path. |
| Threshold | `0.7` | Number from 0 through 1. |
| Trigger frames | `3` | Integer from 1 through 20. |
| Cooldown | `2` | Integer from 0 through 30 seconds. |
| Echo cancellation / noise suppression | enabled | Applied when the Kinect capture service next starts. |
| Logging level | `Information` | Persisted per user; takes effect on the next application launch. |
| Launch at sign-in | disabled | Adds/removes a value in `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`; no administrator access is required. |

Non-secret settings are stored in `%LOCALAPPDATA%\OpenClaw\KinectSatellite\settings.json`. The encrypted token is in `token.dat`, logs are in `logs`, and defaults remain in the source-controlled `appsettings.json` without credentials.

Downloaded wake-word assets are stored beneath the per-user `models` directory. Each entry contains `metadata.json` with its SHA-256 and official URL, `ATTRIBUTION.txt`, and the upstream `LICENSE`. Cached bytes are checked against that SHA-256 before every load. v0.1.0 reports a clear error when an official manifest supplies TensorFlow Lite rather than ONNX; it never renames or substitutes an incompatible file.
