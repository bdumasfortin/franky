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
nicely in an initial physical check. A broader quiet-room test at roughly 20
inches later found that detection sometimes required about ten repetitions.
The synthetic false-rejection result therefore does not represent the current
user/room experience. A later 96% versus 87% physical comparison produced about
three detections per ten attempts at both cutoffs, with reported positive peaks
ranging from 49% to 99% and several attempts producing no completed score. The
model/input mismatch requires representative physical positives; a lower cutoff
alone is not an acceptable production fix.

The first private post-AFE corpus is now complete: 30 positive “Yo Franky”
samples and 20 hard negatives, all captured through the guided control-board
workflow. Offline evaluation of the original deployed model detected 25/30 positives and
activated on 5/20 hard negatives at 96%. Across tested cutoffs from 50–99%, the
best positive count was 28/30 while hard-negative activations never fell below
4/20. Five similar negatives scored 97–100%. This overlap confirms that no
tested threshold is an acceptable fix. After correcting a diagnostic scoring
race, three spoken samples matched board/offline peaks exactly at 100/100,
100/100, and 91/91, validating the absolute offline scores for this pipeline.

Two physically tuned models were then trained in isolated ignored directories.
Candidate v1 recovered all positives but left 5/20 hard-negative activations at
96%, so it was rejected. Candidate v2 scored all 30 positives at 100% and
reduced that count to 2/20. Its separate synthetic/ambient test reported 2.0%
false rejection and 0.187 estimated false accepts per hour at 95%, and 3.33%
false rejection with no observed false accepts at 98%. Because the physical
figures reuse training data, candidate v2 remains provisional. The user chose
to flash it for a pragmatic physical trial at 96%; the original baseline model
remains available for rollback.

The September 3 practical trial was not sufficient: the user reported roughly
one wake in five, plus the activation beep appearing in every successful
transcript. Candidate v2 is not accepted. Investigate live streaming behavior
and generalization separately from the frozen-clip scoring sanity check; the
[session handoff](../../docs/development/session-handoff-2026-09-03.md) records
the next investigation and exact local artifacts.

## Tuning loop

The first on-board threshold is intentionally conservative. Improve the model
with recordings from the actual room and hard negatives discovered during use,
then retrain and export. Keep real recordings and generated artifacts out of
Git.

The control board's **Dataset** area now implements that collection step. It
uses a deliberate three-second firmware diagnostic capture of the same
post-ESP-SR-AFE mono stream passed to the wake model. Samples remain in browser
memory until explicitly kept, then are stored below the ignored
`.cache/recordings/{positive,hard-negative}` directories with JSON sidecars.
They are not automatically mixed into synthetic training data.

Evaluate the deployed model before retraining:

```powershell
.\.venv\Scripts\python.exe .\evaluate_recordings.py
```

Prepare the approved physical corpus and reproduce candidate v2 without
overwriting the deployed firmware model:

```powershell
.\.venv\Scripts\python.exe .\prepare_dataset.py --only-physical-corpus
.\train_physical_candidate.ps1 -Candidate v2
.\.venv\Scripts\python.exe .\evaluate_recordings.py `
  --model .\.cache\trained\yo_franky_physical_v2\tflite_stream_state_internal_quant\stream_state_internal_quant.tflite `
  --output .\.cache\evaluation\physical-candidate-v2.json
```

The preparation step requires exactly 30 positive and 20 hard-negative corpus
samples with valid sidecar hashes and canonical post-AFE audio. It excludes all
parity samples. The training wrapper verifies the preserved rollback-baseline hash, seeds
an isolated candidate from its checkpoint, and refuses partial output. It never
copies the candidate into `firmware/`; export and flashing remain separate,
explicit actions. Candidate v2 was copied and flashed only after separate user
authorization. The default preparation recipe is v2; pass
`--physical-candidate v1` only to reproduce the rejected v1 experiment with its
original unaligned, randomly truncated negative augmentation.

The evaluator reproduces the three-frame inference stride and five-score
integer moving average used by firmware, including the firmware's integer
rounding rule. Parity samples retain the embedded board peak in their sidecar;
the evaluator reports exact, within-one-point, and maximum score deltas without
mixing those samples into the 30/20 corpus totals. Measurements explicitly
marked invalid are preserved but excluded from parity summaries. Board-score
comparison is enabled only for the exact baseline model hash used to capture the
existing parity set; changing the file at the default model path cannot produce
a misleading cross-model comparison. Its private
report is written to `.cache/evaluation/latest.json`. The complete first
physical corpus has exercised the path; the report remains private with the
recordings. See the
[collection and evaluation guide](../../docs/development/wake-word-data-collection.md).
