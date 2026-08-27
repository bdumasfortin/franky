# OpenAI API Development Notes

The computer runtime can use the OpenAI Responses API for conversation and structured tool calls. This is an application integration, so it requires an OpenAI API project, API key, and API-platform billing or credits. A ChatGPT subscription is a separate product and is not an application credential.

Create and manage the key using the [official API quickstart](https://developers.openai.com/api/docs/quickstart). API requests authenticate with a bearer key as described in the [official API reference](https://developers.openai.com/api/reference/overview#authentication).

## Local setup

Set the key only in the environment of the process that needs it:

```powershell
$env:OPENAI_API_KEY = "your-api-key"
$env:FRANKY_ASSISTANT_PROVIDER = "openai"
dotnet run --project services/Franky.Runtime
```

To use the same provider for wake transcripts in the control board, start it
from that shell:

```powershell
$env:OPENAI_API_KEY = "your-api-key"
.\tools\franky-control-board\serve.ps1 -AssistantProvider openai
```

The Wake area reports **Ready · tools** when model-selected commands are
enabled. Selecting `openai` without a key fails at startup instead of silently
falling back. Use the default local Ollama provider or explicitly select `demo`
when cloud access is not wanted.

Optional settings:

- `FRANKY_OPENAI_MODEL` selects the Responses API model.
- `FRANKY_OPENAI_BASE_URL` overrides the API root for testing or a compatible provider.

The previous `ASSISTANT_OPENAI_MODEL` and `ASSISTANT_OPENAI_BASE_URL` names remain accepted as compatibility fallbacks.

Do not commit a key, paste it into a prompt, or write it into project configuration.

## Conversation state and privacy

The first implementation sends `store: true` and continues a conversation with `previous_response_id`. OpenAI currently documents at least 30 days of application-state retention for stored Responses API data in its [data controls guide](https://platform.openai.com/docs/models/default-usage-policies-by-endpoint). Treat household conversation as data leaving the local network, review the current policy before real use, and avoid sensitive conversations during development.

Before calling the cloud-ready voice path complete, make retention behavior a
visible configuration decision. Local-only Ollama mode is now the default
control-board path. Runtime diagnostics intentionally exclude prompt and
response text by default.
