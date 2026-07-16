# Wake-word model notices

OpenClaw Kinect Satellite uses the official **openWakeWord v0.5.1 ONNX** assets for functional Windows wake-word inference:

- Source: <https://github.com/dscripka/openWakeWord>
- Release: <https://github.com/dscripka/openWakeWord/releases/tag/v0.5.1>
- Shared feature models: `melspectrogram.onnx` and `embedding_model.onnx`
- Classifiers: `alexa_v0.1.onnx` and `hey_jarvis_v0.1.onnx`

The application downloads the pinned release assets on first use. Each asset is downloaded twice; matching SHA-256 digests are required before installation. The exact URLs and computed hashes are stored in `ATTRIBUTION.txt`, while `metadata.json` is used to verify every cached file before later loads.

The openWakeWord source code is Apache-2.0. The upstream project states that its included pre-trained models are licensed under **Creative Commons Attribution-NonCommercial-ShareAlike 4.0 International (CC BY-NC-SA 4.0)**. These models therefore require attribution, are limited to non-commercial use, and require adaptations to be shared under the same license. The legal code is downloaded beside each installed model bundle:

<https://creativecommons.org/licenses/by-nc-sa/4.0/legalcode>

ESPHome/OHF MicroWakeWord Alexa and Jarvis assets are TensorFlow Lite Micro models and are not used or relabeled as ONNX. The Windows implementation is accurately identified as openWakeWord because its official ONNX models are compatible with ONNX Runtime on win-x86.
