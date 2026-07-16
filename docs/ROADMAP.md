# Development roadmap

## Milestone 1 — developer preview (current)

- Clean architecture and DI composition
- Kinect SDK v1 adaptive beam capture with audio processing
- Functional openWakeWord v0.5.1 ONNX streaming inference with Alexa/Jarvis
- Home Assistant Assist WebSocket streaming and TTS playback
- Structured console/file logging and orchestration test
- Native WPF configuration, service controls, validation, connection test, and tray menu
- DPAPI-protected per-user secrets, launch-at-sign-in, and self-contained x86 publishing

## Milestone 2 — protocol hardening

- Explicit voice-activity endpointing and audio-stream completion
- Persistent WebSocket with exponential reconnect and health state
- Unit fixtures for Assist events, malformed responses, and timeouts
- Configurable output device and playback volume
- Graceful handling of Kinect disconnect/reconnect

## Milestone 3 — wake-word expansion

- Optional native TensorFlow Lite Micro adapter for ESPHome MicroWakeWord assets
- Model calibration utility and additional properly licensed catalogs
- Corpus-based false-accept/false-reject benchmarks

## Milestone 4 — View Assist experience

- Register satellite/device entities and expose availability
- Optional camera snapshots/context for multimodal View Assist flows
- LEDs/sounds for listening, thinking, and error states
- Windows Service installer, signed packages, diagnostics bundle, and hardware-in-the-loop CI
