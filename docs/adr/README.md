# Architecture Decision Records

ADRs document consequential technical decisions and their tradeoffs.

Statuses used in this repository:

- **Proposed** — under review and not authorization to implement.
- **Accepted** — explicitly approved and currently authoritative.
- **Superseded** — replaced by a later ADR.
- **Rejected** — considered but not selected.

Create new records from [`0000-template.md`](0000-template.md). Do not rewrite the historical decision in an accepted ADR; add a superseding ADR when the decision changes.

## Index

- [ADR-0001: Build a standalone custom assistant](0001-runtime-and-home-automation-boundary.md) — Accepted.
- [ADR-0002: Use the ESP32 as a Wi-Fi voice satellite](0002-use-esp32-as-wifi-voice-satellite.md) — Accepted.
- [ADR-0003: Use a .NET modular monolith for the computer runtime](0003-use-dotnet-modular-monolith.md) — Accepted.
- [ADR-0004: Use WebSocket, JSON control messages, and PCM audio](0004-use-websocket-json-and-pcm.md) — Accepted.
- [ADR-0005: Use the OpenAI API behind a replaceable conversation provider](0005-use-openai-api-behind-provider.md) — Accepted with prerequisite.
- [ADR-0006: Expose only allowlisted named commands to the model](0006-allowlisted-command-tools.md) — Accepted.
- [ADR-0007: Use local Whisper for speech-to-text](0007-use-local-whisper-for-speech-to-text.md) — Accepted.
- [ADR-0008: Use a custom microWakeWord model for “Yo Franky”](0008-use-custom-microwakeword-model-for-yo-franky.md) — Accepted.
