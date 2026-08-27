# ADR-0007: Use local Whisper for speech-to-text

- Status: Accepted
- Date: 2026-08-26
- Deciders: Project owner through the Franky speech-to-text Lavish review

## Context

Franky now detects the built-in “Hi ESP” wake phrase and captures good-quality stereo audio from the Waveshare board. The next increment should replace the fixed five-second wake recording with a natural utterance that ends after the user stops speaking, then show the recognized text without executing commands.

Speech transcription can run on the computer, in the browser, or through a cloud API. This computer has an NVIDIA RTX 3070 Ti and can run an appropriately sized Whisper model locally. Household speech should not leave the computer merely to prototype the command pipeline.

## Decision

- Use the ESP-SR Audio Front End voice-activity result on the ESP32 to detect speech start and trailing silence.
- Preserve a short VAD pre-roll, allow four seconds for speech to begin, end after roughly 900 ms of trailing silence, and enforce a 20-second speech cap.
- Send the completed wake utterance as 16 kHz, 16-bit, mono PCM to the computer. Keep the existing raw-stereo manual Audio test path unchanged.
- Define a replaceable speech-transcriber boundary in the .NET runtime.
- Implement the first provider with Whisper.net and the `small.en` Whisper model.
- Prefer the CUDA runtime when its native prerequisites are available and include the CPU runtime as a fallback.
- Cache the downloaded model under the user's local application-data directory, outside the repository.
- Serve the Franky control board and a loopback-only transcription endpoint from the same .NET process.
- Show the latest transcript in the Wake interface and terminal. Do not add wake utterances to Recent recordings.
- Keep wake audio and transcript text in memory only for this increment. Do not write either to diagnostics or durable storage.
- Do not interpret the transcript or execute commands in this increment.

## Consequences

### Positive

- Wake utterance audio and transcript text remain local.
- The transcriber boundary can later feed the approved conversation and command pipeline without moving speech recognition out of the browser.
- The board supplies a natural endpoint before uploading a bounded clip.
- The GPU can accelerate transcription when the matching CUDA runtime is available, while CPU fallback keeps the feature usable.

### Negative

- The first run downloads a roughly 466 MiB model.
- Local model startup uses computer memory and may fall back to slower CPU inference when CUDA native dependencies are unavailable.
- Voice-activity timing must be tuned against the real room and device; a noisy room can still reach the safety cap.
- The current USB serial control board remains a development path. The accepted Wi-Fi/WebSocket satellite transport is still future work.

## Alternatives considered

- **OpenAI Audio transcription API:** less local model setup, but every utterance crosses the local privacy boundary and requires API credentials and billing.
- **Browser-only Whisper:** keeps audio local, but couples transcription to browser lifecycle and would later need to move into the computer runtime before command execution.
