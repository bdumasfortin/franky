# Version 1 Implementation Plan

Status: **Approved direction; implementation in progress**

## Outcome

Build one safe, understandable loop from a spoken request to a spoken response:

```text
wake → listen → transcribe → understand → act or answer → speak
```

The ESP32 owns the room-facing audio and status experience. The computer owns
speech recognition, conversation, capability policy, integrations, and response
generation.

## Completed foundations

### Computer runtime

- .NET 10 modular-monolith scaffold with explicit internal boundaries.
- Deterministic demo, local Ollama, and optional OpenAI Responses API conversation providers.
- Provider-specific in-memory conversation continuation behind one interface.
- Strictly allowlisted read-only command execution.
- Automated checks for command validation, conversation continuation, and tool results.

### Physical board and control surface

- Factory image backed up before the first write.
- Custom Franky firmware built and flashed with ESP-IDF 5.5.2.
- Stereo microphone capture verified from close range to roughly 12 feet.
- Speaker cues and seven-pixel state LEDs verified.
- Local WakeNet9 **“Hi ESP”** detection verified.
- Reproducible custom **“Yo Franky”** microWakeWord training workspace, quantized
  model, TensorFlow Lite Micro integration, and WakeNet fallback implemented.
- Model-enabled firmware built, flashed, booted, observed idle without watchdog
  faults, and verified through a successful physical spoken test.
- Voice-activity endpointing and bounded mono wake capture verified.
- Local Whisper `small.en` transcription working with NVIDIA GPU acceleration and CPU fallback.
- State-driven browser control board working over USB serial.
- Loopback assistant-turn endpoint connects wake transcripts to the existing
  conversation session and named-command tool loop.
- Structured action outcomes and Franky replies render separately in the
  control board; demo mode is explicitly labeled as unable to select tools.
- Assistant bridge, session continuity, busy-turn rejection, and tool-call
  reporting are locally covered for the OpenAI and Ollama request shapes.
- Live `qwen3.5:4b` selected and executed both read-only diagnostics through the
  loopback endpoint. Background model preloading avoids the observed cold-load
  penalty; the physical spoken path remains unverified.
- A cleaned, metadata-free 16 kHz mono “SUUUPER” asset is embedded in firmware.
- `SFX frankys_suuuper` has explicit start/completion acknowledgements, and the
  browser maps only the fixed `device.sfx.frankys_suuuper` action to it.
- Both conversation providers describe the natural “How is it going?” intent,
  and the UI waits for board completion rather than treating model selection as
  playback success.
- The image is flashed, a direct serial request returned `SFX_START` and
  `SFX_DONE`, and live `qwen3.5:4b` selected the exact device action for “How is
  it going?” A physical “How's it going?” test then returned text with no action,
  so common short variants now route through a narrow deterministic matcher
  before the model. Audible playback through the corrected browser path still
  requires user observation.

## Active vertical slice

Prove the implemented bridge and first device action on the physical voice path:

1. Run both read-only diagnostic requests through the physical wake path and
   record observed latency while Whisper and Ollama share the GPU.
2. Confirm the direct `SFX frankys_suuuper` playback was audible; its firmware
   start and completion acknowledgements are already verified.
3. Say “Yo Franky”, then “How is it going?”, and verify the already-tested local model selects
   the fixed action and the UI reports success only after playback.
4. Keep wake audio and transcripts ephemeral unless persistence is explicitly designed later.

## Following increments

1. Add text-to-speech behind a replaceable provider boundary.
2. Stream or send the generated response to the board speaker.
3. Extend the draft protocol for wake-triggered utterances and named playback.
4. Implement authenticated Wi-Fi/WebSocket transport and reconnect behavior.
5. Tune the custom “Yo Franky” model with real-room positives or hard negatives
   only if ordinary use exposes misses or false activations.

## Validation gates

- Release build, formatting check, and automated runtime tests pass.
- Wake capture ends naturally and does not grow without a hard limit.
- Local transcription remains local and does not persist speech by default.
- A model-requested command can execute only a fixed allowlisted process.
- Unknown commands and arbitrary arguments are rejected.
- API, model, device, and command failures produce truthful state and useful diagnostics.
- The speaker never loops a cue after a state transition or disconnect.
- Wi-Fi behavior remains marked unverified until exercised on the physical board.

## Deferred beyond v1

- Multiple ESP32 satellites.
- Persistent personal memory and multiple users.
- General-purpose autonomous behavior.
- Unrestricted or unconfirmed state-changing commands.
- Production-grade acoustic tuning across different rooms.
