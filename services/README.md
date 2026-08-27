# Franky runtime

[`Franky.Runtime/`](Franky.Runtime/) is the project-owned .NET 10 application that runs on the computer. It contains the replaceable conversation, capability, diagnostics, command, and speech-transcription boundaries, and it serves the local browser control board during USB development.

Control-board mode exposes loopback-only transcription and assistant-turn
endpoints. A completed wake transcript reuses the same conversation session and
strictly allowlisted tool loop as the text console. It returns action outcomes
separately from assistant prose so the UI does not confuse a generated claim
with a command result.

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
