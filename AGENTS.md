# Franky Project Instructions

## Project boundary

Franky is a personal, from-scratch voice assistant built from:

- custom firmware on a Waveshare ESP32-S3-AUDIO-Board; and
- a custom .NET runtime on the computer for speech, conversation, safe commands, and future home integrations.

The working development path uses USB serial. Microphone capture, **“Hi ESP”**
fallback wake detection, voice-activity endpointing, speaker cues, status LEDs,
and local Whisper transcription are verified. A custom **“Yo Franky”** model is
trained, flashed, boot-stable, and verified in an initial physical spoken test.
Longer-term miss and false-activation behavior is still being observed.
The loopback control-board service now connects completed transcripts to the
existing conversation and allowlisted-command path, with structured action
outcomes and an honest demo/no-tools fallback. Local Ollama with `qwen3.5:4b`
is the selected conversation provider, with the OpenAI adapter retained for a
future cloud switch. Live Ollama tool selection is verified through the
loopback endpoint; the physical spoken-command path still needs verification. Authenticated
Wi-Fi/WebSocket transport, on-board named SFX playback, and spoken responses
remain planned. Franky does not depend on the official Home Assistant platform.

## Working rules

- Prefer the smallest useful end-to-end slice. Preserve clear boundaries between firmware, transport, speech, conversation, capabilities, and UI.
- Keep firmware under `firmware/`, computer code under `services/`, tests under `tests/`, and development tools under `tools/`.
- Never allow model text to become arbitrary shell input. Actions must use explicit, allowlisted capabilities with truthful outcomes.
- Keep credentials, Wi-Fi details, API tokens, model files, factory backups, recordings, transcripts, and generated user data out of Git.
- Make privacy boundaries explicit whenever audio or text can leave the computer or local network.
- Add useful diagnostics at device connection, capture, transcription, intent, action, and speech-output boundaries without logging private content by default.
- Treat explicit user feedback and accepted ADRs as authoritative. Keep unresolved choices visibly provisional.
- Record consequential architecture decisions under `docs/adr/`. Do not rewrite an accepted ADR to fit later work; add a superseding ADR. A proposed ADR becomes accepted only after explicit approval.

## Documentation is part of the change

- Update the relevant documentation in the same change as every meaningful code, firmware, behavior, setup, or architecture change. Do not leave documentation cleanup for later.
- Keep `README.md` concise and accurate about what works today, what is planned, how to run Franky, and the privacy boundary.
- Keep `docs/README.md` useful as the documentation index. Update product, architecture, plan, development, and hardware records when their facts change.
- Record physical-board claims only when manually observed. Clearly label results as built, tested, manually observed, inferred, or unverified.
- Preserve historical ADRs and remove stale statements from living documents.
- Verify project-authored local Markdown links and image references after documentation changes.

## Validation

Run checks proportionate to the change and report what was run or skipped. The standard computer-side checks are:

```powershell
dotnet build Franky.slnx --configuration Release
dotnet run --project tests/Franky.Runtime.Tests --configuration Release
dotnet format Franky.slnx --verify-no-changes
node --check tools/franky-control-board/app.js
```

Build firmware changes with the pinned ESP-IDF environment before flashing. Test relevant failure paths as well as the happy path, especially device disconnects, unavailable services, missing credentials, bounded audio capture, and rejected commands.
