# Development Workflow

## Current state

The .NET 10 runtime supports local Ollama conversation, deterministic demo and
optional OpenAI providers, two allowlisted read-only commands, and local
Whisper transcription. The custom Franky firmware is running on the physical board.
Stereo capture, WakeNet “Hi ESP” detection, voice-activity endpointing, speaker
cues, and animated status LEDs have all been observed through the USB
development control board. A locally trained “Yo Franky” microWakeWord image is
also flashed, has booted stably, and has passed its first physical spoken test.
A later quiet-room check at roughly 20 inches found that it sometimes required
about ten repetitions. Wake sensitivity is therefore unacceptable and must be
tuned from physical evidence rather than synthetic metrics alone. The first
private physical corpus is now complete. Offline scoring found overlapping
positive and hard-negative distributions at every tested cutoff from 50–99%,
so a threshold change is not a viable fix. The corrected matched-score firmware
now captures first and scores the frozen post-AFE buffer without concurrent
model access. Three spoken samples matched the offline evaluator exactly at
100/100, 100/100, and 91/91. Two isolated candidates were then trained without
overwriting the deployed model. The second candidate retained all 30 physical
positives at 100% and reduced 96%-cutoff hard-negative activations from 5/20 to
2/20 on the reused training corpus. This is provisional evidence only. The user
explicitly chose to flash candidate v2 for a pragmatic trial before the formal
fresh-session gate; the board booted with it armed at 96%, and the original
baseline remains available for rollback.

The subsequent September 3 trial failed: the user reported roughly one wake in
five and the activation cue appearing in every successful transcript. Work is
paused, not accepted. The [session handoff](session-handoff-2026-09-03.md)
records the investigation priorities, current artifacts, and restart procedure.

The active gap is no longer basic hardware bring-up or transcript routing. The
control-board service now feeds each completed wake transcript into the existing
conversation and safe-command pipeline and returns separate action and reply
results. This bridge and both real-provider request shapes are locally tested.
Live Ollama selected and executed both read-only diagnostics through the
loopback endpoint. After successful physical wakes, both diagnostics answered
correctly and the longer negative control behaved as expected. The first
requested device capability—named “SUUUPER” SFX playback—is implemented and
flashed with an explicit board completion acknowledgement. The board returned
both acknowledgements for a direct request, and the user later heard it through
the physical wake path with truthful status. Because an earlier contraction
variant missed the action, common short variants route locally before the
model. Generated spoken responses follow.
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
- Use the control board's Dataset area for approved physical wake samples. It
  stores only explicitly kept recordings under the ignored wake-word cache;
  ordinary wake utterances remain ephemeral. After corpus completion, parity
  samples are labeled separately and excluded from the corpus totals.
- Keep Wi-Fi transport changes behind the documented device boundary so the working speech and command paths do not need to be redesigned.

See the [hardware bring-up record](../../firmware/hardware-bring-up.md) for observed evidence and remaining hardware gaps.

## Local Ollama evidence

On the current RTX 3070 Ti development computer, an explicit cold model request
took about 65 seconds. Background startup preloading reduced model preparation
to about 4.9 seconds; subsequent two-round tool requests completed in roughly
0.9–1.7 seconds. Both `runtime.dotnet_version` and `system.identity` were
selected by the live `qwen3.5:4b` model and returned truthful tool outcomes.
The cold/preload timings above are loopback-only observations. During the later
physical check, runtime logs recorded 164–439 ms local transcription processing
and 4–1,046 ms assistant-turn processing across four non-empty requests. These
do not include wake detection, capture, transfer, or user-perceived completion.

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
