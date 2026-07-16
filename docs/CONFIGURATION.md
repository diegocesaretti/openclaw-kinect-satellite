# Configuration reference

Settings are loaded from `appsettings.json`, environment variables, and command-line configuration providers. Use double underscores in environment keys, for example `HomeAssistant__AccessToken`.

| Key | Default | Description |
| --- | --- | --- |
| `Kinect:EchoCancellation` | `true` | Enables Kinect SDK acoustic echo cancellation. |
| `Kinect:NoiseSuppression` | `true` | Enables Kinect SDK noise suppression where exposed by the installed driver. |
| `Kinect:FrameMilliseconds` | `30` | PCM frame duration; the bundled frontend expects 30 ms. |
| `WakeWord:ModelPath` | `models/okay-nabu.onnx` | Local ONNX model path. |
| `WakeWord:Threshold` | `0.7` | Minimum output probability in the range 0–1. |
| `WakeWord:TriggerFrames` | `3` | Consecutive positive evaluations needed to activate. |
| `WakeWord:CooldownSeconds` | `2` | Suppression period after activation. |
| `HomeAssistant:Url` | `http://homeassistant.local:8123` | Home Assistant base HTTP(S) URL; WebSocket scheme is derived. |
| `HomeAssistant:AccessToken` | invalid placeholder | Long-lived access token; supply securely. |
| `HomeAssistant:PipelineId` | `null` | Pipeline ID, or preferred pipeline when omitted. |
| `HomeAssistant:DeviceId` | `null` | Optional registered Home Assistant device ID. |

