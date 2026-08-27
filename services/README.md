# Franky runtime

[`Franky.Runtime/`](Franky.Runtime/) is the project-owned .NET 10 application that runs on the computer. It contains the replaceable conversation, capability, diagnostics, command, and speech-transcription boundaries, and it serves the local browser control board during USB development.

[ADR-0001](../docs/adr/0001-runtime-and-home-automation-boundary.md) accepts a standalone custom assistant. [ADR-0003](../docs/adr/0003-use-dotnet-modular-monolith.md) selects a .NET 10 modular monolith with explicit internal boundaries for conversation, capabilities, device transport, and diagnostics.

## Run

Local text demo without credentials:

```powershell
dotnet run --project services/Franky.Runtime -- --demo
```

Control-board and local Whisper service:

```powershell
.\tools\franky-control-board\serve.ps1
```

Optional cloud conversation setup is documented in the [OpenAI API development notes](../docs/development/openai-api.md).
