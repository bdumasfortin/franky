# Development Workflow

## Current state

The .NET 10 runtime supports deterministic local conversation, an optional OpenAI provider, two allowlisted read-only commands, and local Whisper transcription. The custom Franky firmware is running on the physical board. Stereo capture, WakeNet detection, voice-activity endpointing, speaker cues, and animated status LEDs have all been observed through the USB development control board.

The active gap is no longer basic hardware bring-up. The next vertical slice is to feed a wake transcript into the conversation and safe-command pipeline, produce a response, and then return spoken audio. Wi-Fi/WebSocket transport remains the intended room deployment but is not implemented yet.

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

- Use `--demo` to exercise conversation and safe command execution without API credentials or the board.
- Use the Franky control board to exercise the physical microphones, speaker, LEDs, wake engine, and local transcription over USB.
- Keep Wi-Fi transport changes behind the documented device boundary so the working speech and command paths do not need to be redesigned.

See the [hardware bring-up record](../../firmware/hardware-bring-up.md) for observed evidence and remaining hardware gaps.

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
