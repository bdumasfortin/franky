# Yo Franky wake-word training

This tool trains a small, streaming **“Yo Franky”** detector for the ESP32-S3.
It uses the Apache-licensed microWakeWord training framework and keeps Franky's
existing ESP-SR Audio Front End for microphone processing and voice activity.
The generated model runs locally on the board through TensorFlow Lite Micro.

## Local-only artifacts

The virtual environment, generated speech, downloaded ambient feature sets,
training checkpoints, and `.tflite` model stay below `.venv/` or `.cache/` and
are ignored by Git. The exported firmware model is also ignored. Generated
speech and training audio are not sent to Franky's runtime or cloud APIs.

The upstream ambient feature archives combine datasets with different license
terms. Treat the resulting model as suitable for this non-commercial personal
project unless every source dataset is audited separately.

## Build a model

Use Python 3.10. On Windows, `bootstrap.ps1` installs CUDA-enabled PyTorch for
synthetic sample generation. TensorFlow model training itself runs on CPU;
`-CpuOnly` avoids the CUDA PyTorch package when no NVIDIA GPU is available.

```powershell
cd tools/wake-word
.\bootstrap.ps1
.\.venv\Scripts\python.exe .\prepare_dataset.py
.\train.ps1
.\export_model.ps1
```

`prepare_dataset.py` creates 3,000 positive examples across synthetic speakers,
1,500 deliberately similar negative phrases, augmented feature maps, and the
official microWakeWord ambient negative sets. Existing complete outputs are
reused. Partial outputs are never deleted automatically.

The firmware build detects
`firmware/franky-device/main/models/yo_franky.tflite` automatically. If that
ignored file is absent, it retains the built-in **“Hi ESP”** WakeNet fallback.

## Current model evidence

The first local model is 62,304 bytes with SHA-256
`987223a0697b9f8a382f6f00cc523026478ba99a21cef264e3686fd887b203dd`.
Held-out streaming evaluation with a five-result moving average measured a
1.33% false-rejection rate and 0.187 estimated false accepts per hour at the
selected 0.96 cutoff. These figures are synthetic and ambient-dataset evidence,
not a substitute for spoken tests on the physical board.

The model-enabled firmware and the model-absent WakeNet fallback both compile
with ESP-IDF 5.5.2. The custom image has booted on the physical board, reported
`WAKE_ENGINE microwakeword yo_franky`, and remained idle without watchdog
faults. The user then confirmed that spoken “Yo Franky” detection worked very
nicely on the physical board. Longer-term false-activation and miss behavior is
not yet measured.

## Tuning loop

The first on-board threshold is intentionally conservative. Improve the model
with recordings from the actual room and hard negatives discovered during use,
then retrain and export. Keep real recordings and generated artifacts out of
Git.
