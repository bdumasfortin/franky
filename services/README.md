# Franky runtime

[`Franky.Runtime/`](Franky.Runtime/) is the project-owned .NET 10 application that runs on the computer. It contains the replaceable conversation, capability, diagnostics, command, speech-transcription, and speech-synthesis boundaries, and it serves the local browser control board during USB development.

The speech-synthesis boundary currently defines and tests one bounded output
contract: 16 kHz, 16-bit, mono PCM, with single-flight execution, cooperative
cancellation, and structured diagnostics that record lengths and outcomes but
not response text. No production TTS engine or Franky voice is selected yet,
and generated audio is not wired to the board.

Control-board mode exposes loopback-only transcription and assistant-turn
endpoints. A completed wake transcript reuses the same conversation session and
strictly allowlisted tool loop as the text console. It returns action outcomes
separately from assistant prose so the UI does not confuse a generated claim
with a command result.

The same loopback host provides the explicitly approved wake-dataset store.
It accepts only bounded canonical 16 kHz mono PCM WAV samples, requires a
per-process mutation token, generates every filename itself, and writes only
below the ignored local wake-word cache. Sample metadata excludes transcripts
and speaker identity, and the API supports explicit deletion.

The control-board host also exposes a fixed device-action tool. It accepts only
`device.sfx.frankys_suuuper`; the browser translates that semantic action to a
fixed serial command and waits for the ESP32 acknowledgement. The model never
supplies arbitrary serial text.

Common short variants of “How is it going?” are recognized locally before the
conversation provider. This makes Franky's signature response deterministic
without hijacking longer questions such as “How is it going with the build?”

[ADR-0001](../docs/adr/0001-runtime-and-home-automation-boundary.md) accepts a standalone custom assistant. [ADR-0003](../docs/adr/0003-use-dotnet-modular-monolith.md) selects a .NET 10 modular monolith with explicit internal boundaries for conversation, capabilities, device transport, and diagnostics.

## Run

Local text conversation with Ollama (after the one-time setup):

```powershell
$env:FRANKY_ASSISTANT_PROVIDER = "ollama"
dotnet run --project services/Franky.Runtime
```

Control-board and local Whisper service:

```powershell
.\tools\franky-control-board\serve.ps1
```

Local setup and provider switching are documented in the [Ollama development
notes](../docs/development/local-ollama.md). Optional cloud conversation setup
is documented in the [OpenAI API development notes](../docs/development/openai-api.md).
