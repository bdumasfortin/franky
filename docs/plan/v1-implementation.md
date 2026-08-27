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
- Deterministic local conversation provider and optional OpenAI Responses API provider.
- In-memory conversation continuation.
- Strictly allowlisted read-only command execution.
- Automated checks for command validation, conversation continuation, and tool results.

### Physical board and control surface

- Factory image backed up before the first write.
- Custom Franky firmware built and flashed with ESP-IDF 5.5.2.
- Stereo microphone capture verified from close range to roughly 12 feet.
- Speaker cues and seven-pixel state LEDs verified.
- Local WakeNet9 **“Hi ESP”** detection verified.
- Voice-activity endpointing and bounded mono wake capture verified.
- Local Whisper `small.en` transcription working with NVIDIA GPU acceleration and CPU fallback.
- State-driven browser control board working over USB serial.

## Active vertical slice

Connect the wake transcript to the existing conversation and safe-command path:

1. Treat one completed transcript as a user request.
2. Show the transition through listening, processing, success, and error states.
3. Return a text response to the control board.
4. Preserve the allowlisted command boundary and truthful failure behavior.
5. Keep wake audio and transcripts ephemeral unless persistence is explicitly designed later.

## Following increments

1. Add text-to-speech behind a replaceable provider boundary.
2. Stream or send the generated response to the board speaker.
3. Extend the draft protocol for wake-triggered utterances.
4. Implement authenticated Wi-Fi/WebSocket transport and reconnect behavior.
5. Replace the built-in development wake phrase with the selected Franky phrase.

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
