# Development Workflow

## Current state

The .NET 10 runtime supports local Ollama conversation, deterministic demo and
optional OpenAI providers, two allowlisted read-only commands, and local
Whisper transcription. The custom Franky firmware is running on the physical board.
Stereo capture, WakeNet “Hi ESP” detection, voice-activity endpointing, speaker
cues, and animated status LEDs have all been observed through the USB
development control board. A locally trained “Yo Franky” microWakeWord image is
also flashed, has booted stably, and has passed its first physical spoken test.
Longer-term sensitivity and false-activation behavior should be tuned from
ordinary use rather than synthetic metrics alone.

The active gap is no longer basic hardware bring-up or transcript routing. The
control-board service now feeds each completed wake transcript into the existing
conversation and safe-command pipeline and returns separate action and reply
results. This bridge and both real-provider request shapes are locally tested.
Live Ollama selected and executed both read-only diagnostics through the
loopback endpoint. The physical wake-to-command path remains unverified. The
first requested device capability—named “SUUUPER” SFX playback—is implemented
and flashed with an explicit board completion acknowledgement. The board
returned both acknowledgements for a direct request, and live Ollama selected
the action from “How is it going?” Because it missed the contraction “How's it
going?” in a later physical test, common short variants now route locally before
the model. The corrected physical voice path remains unverified. Generated spoken responses follow.
Wi-Fi/WebSocket transport remains the intended
room deployment but is not implemented yet.

## Secrets

Never commit:

- Wi-Fi credentials;
- device addresses or access tokens;
- API keys;
- raw audio recordings;
- transcripts containing household information; or
- generated runtime state.

Supply local values through process environment variables. Do not place a real API key in a tracked or untracked project file merely for convenience.

## Decision flow

1. Capture product behavior and constraints in `docs/product/`.
2. Record consequential technical choices in `docs/adr/`.
3. Keep uncertain ADRs `Proposed`.
4. Implement only the approved slice.
5. Record verification evidence and remaining unknowns.

## Development paths

- Use `--demo` to exercise conversation flow without API credentials or the board;
  demo mode does not select or execute tools.
- Use `FRANKY_ASSISTANT_PROVIDER=ollama` for the local tool-capable provider;
  the control-board launcher selects it by default.
- Use the Franky control board to exercise the physical microphones, speaker,
  LEDs, wake engine, local transcription, and model-selected named-command path
  over USB. Select `openai` explicitly only when its separate API key is configured.
- Use `tools/wake-word` to reproduce the ignored local “Yo Franky” model; never
  commit its datasets, recordings, checkpoints, or exported `.tflite` file.
- Keep Wi-Fi transport changes behind the documented device boundary so the working speech and command paths do not need to be redesigned.

See the [hardware bring-up record](../../firmware/hardware-bring-up.md) for observed evidence and remaining hardware gaps.

## Local Ollama evidence

On the current RTX 3070 Ti development computer, an explicit cold model request
took about 65 seconds. Background startup preloading reduced model preparation
to about 4.9 seconds; subsequent two-round tool requests completed in roughly
0.9–1.7 seconds. Both `runtime.dotnet_version` and `system.identity` were
selected by the live `qwen3.5:4b` model and returned truthful tool outcomes.
These are loopback service observations, not yet physical spoken-path results.

## Validation

```powershell
dotnet build Franky.slnx --configuration Release
dotnet run --project tests/Franky.Runtime.Tests --configuration Release
dotnet run --project services/Franky.Runtime --configuration Release -- --demo
dotnet format Franky.slnx --verify-no-changes
```

For control-board JavaScript changes, also run:

```powershell
node --check tools/franky-control-board/app.js
```
