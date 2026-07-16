# Wake-word model notices

OpenClaw Kinect Satellite contains catalog metadata for the **Alexa** and **Hey Jarvis** entries published by the ESPHome `micro-wake-word-models` project:

- Source repository: <https://github.com/esphome/micro-wake-word-models>
- Catalog manifests: `models/v2/alexa.json` and `models/v2/hey_jarvis.json`
- Upstream license: <https://github.com/esphome/micro-wake-word-models/blob/main/LICENSE>

The model binaries are **not redistributed in the OpenClaw release**. The upstream artifacts target TensorFlow Lite Micro, while this developer-preview process currently hosts ONNX Runtime. Renaming a `.tflite` artifact to `.onnx` would not make it compatible and is expressly avoided.

On first use, OpenClaw downloads the selected upstream manifest and exact model asset from the URLs declared by that manifest. It downloads the model twice, compares SHA-256 digests, and only then stores it under the current user's Local AppData model directory. It also stores:

- the computed SHA-256 in `metadata.json`;
- the official model and manifest URLs in `metadata.json` and `ATTRIBUTION.txt`; and
- the official upstream `LICENSE` next to the model.

Every later load verifies the cached file against the stored SHA-256. If the upstream format is not ONNX, the application reports the format incompatibility clearly and retains the unmodified verified original. Native TensorFlow Lite Micro execution is tracked as roadmap work; no converted or unofficial model is substituted.

