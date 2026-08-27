# Local Ollama Development

Franky's default interactive conversation provider is Ollama running locally
with `qwen3.5:4b`. The model selects from the same strongly typed, allowlisted
tools as every other provider; it cannot emit arbitrary shell commands.

This is an application runtime choice, not an attempt to reuse a ChatGPT or
Codex login. Ollama needs no account or API key, and transcript text and model
responses remain on the computer.

## Install once

Install [Ollama for Windows](https://docs.ollama.com/windows), then download the
default model:

```powershell
ollama pull qwen3.5:4b
```

The default quantized model is roughly 3.4 GB. It was selected for the current
RTX 3070 Ti with 8 GB of VRAM so it can coexist more comfortably with local
Whisper than an 8B model. The live loopback service has selected both current
read-only commands correctly. Quality and shared-GPU latency still need
physical voice-path observation before that complete path is treated as verified.

## Run

The control-board launcher selects Ollama by default:

```powershell
.\tools\franky-control-board\serve.ps1
```

Equivalent explicit settings are:

```powershell
$env:FRANKY_ASSISTANT_PROVIDER = "ollama"
$env:FRANKY_OLLAMA_MODEL = "qwen3.5:4b"
dotnet run --project services/Franky.Runtime -- --control-board
```

`FRANKY_OLLAMA_BASE_URL` defaults to `http://127.0.0.1:11434/` and is useful
only when Ollama listens elsewhere. Franky preloads the model in the background
at startup and keeps it resident for one hour after each request. Override that
duration with `FRANKY_OLLAMA_KEEP_ALIVE` using an Ollama duration such as `30m`
or `2h`.

## Switch providers

Provider selection is explicit and reversible:

```powershell
# Honest deterministic mode; never selects tools
.\tools\franky-control-board\serve.ps1 -AssistantProvider demo

# Optional cloud mode; requires separate API billing and a key in this shell
$env:OPENAI_API_KEY = "your-api-key"
.\tools\franky-control-board\serve.ps1 -AssistantProvider openai
```

Ollama stores conversation messages only in the in-memory Franky session. The
OpenAI adapter continues to use its own Responses API continuation mechanism.
Both implement the same `IConversationClient` boundary and receive the same
provider-neutral tool definitions, so adding another cloud adapter does not
change the wake, transcription, command, or UI layers.

Do not commit downloaded model files or generated conversation data.
