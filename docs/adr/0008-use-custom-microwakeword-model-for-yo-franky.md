# ADR-0008: Use a custom microWakeWord model for “Yo Franky”

- Status: Accepted
- Date: 2026-08-26
- Deciders: Project owner through direct conversation

## Context

Franky currently uses Espressif's bundled WakeNet9 “Hi ESP” model. The desired
phrase is “Yo Franky,” and Espressif does not publish a self-service WakeNet
training pipeline. The detector must remain local, lightweight enough for the
ESP32-S3, and compatible with the existing ESP-SR audio front end and utterance
capture flow.

## Decision

- Train a phrase-specific streaming model with the Apache-2.0 microWakeWord
  framework and synthetic “Yo Franky” examples.
- Include near-phrase hard negatives such as “frankly,” “Yo Frank,” and “Hey
  Franky,” plus general speech and household-noise features.
- Run the quantized TensorFlow Lite Micro model on the ESP32-S3 after ESP-SR's
  existing 16 kHz mono audio front end.
- Keep training audio, downloaded datasets, checkpoints, and the generated
  `.tflite` model local and ignored by Git. Commit only reproducible scripts,
  phrase lists, configuration, integration code, and documentation.
- Use Espressif's Apache-2.0 TensorFlow Lite Micro component for inference.
  Do not copy or vendor the GPL-3.0 `micro_wake_word_standalone` component into
  this MIT-licensed repository.
- Retain the built-in “Hi ESP” WakeNet model as a build-time fallback when the
  local “Yo Franky” model artifact is absent.

## Consequences

### Positive

- The selected phrase runs entirely on the board and preserves the existing
  local privacy boundary.
- Training and inference are reproducible without depending on a private model
  training service.
- The ESP-SR AFE, voice-activity endpointing, cues, LEDs, and transcription flow
  remain reusable.
- A fallback firmware can still be built from a clean clone before a custom
  model is generated.

### Negative

- Model quality depends heavily on synthetic-data diversity and later recordings
  from the real board and room.
- Training requires several gigabytes of local data and a separate Python tool
  environment.
- The firmware must carry and maintain a small custom feature-extraction and
  streaming-inference layer.
- The ignored model artifact must be regenerated or transferred locally on a new
  development computer.

### Follow-up

- Observe false accepts and missed detections during ordinary physical-board use
  before treating the initial tuning as settled.
- Add real positive and hard-negative recordings only if the first synthetic
  model needs tuning; keep those recordings outside Git.
- Remove the “Hi ESP” fallback only after the custom path has remained reliable
  through ordinary use.

## Implementation evidence

On 2026-08-26, the first quantized model was trained and evaluated locally,
integrated with Espressif TensorFlow Lite Micro 1.3.7, and compiled in both
model-enabled and model-absent fallback builds with ESP-IDF 5.5.2. The custom
image was flashed to the physical board, reported
`WAKE_ENGINE microwakeword yo_franky`, and remained idle without watchdog
faults after a feed-task scheduling correction. The user then confirmed a
successful physical spoken detection. Longer-term false-activation and missed-
detection behavior remain open tuning evidence.
