# OpenAI API Development Notes

The computer runtime can use the OpenAI Responses API for conversation and structured tool calls. This is an application integration, so it requires an OpenAI API project, API key, and API-platform billing or credits. A ChatGPT subscription is a separate product and is not an application credential.

Create and manage the key using the [official API quickstart](https://platform.openai.com/docs/quickstart/make-your-first-api-request). API requests authenticate with a bearer key as described in the [official API reference](https://developers.openai.com/api/reference/overview#authentication).

## Local setup

Set the key only in the environment of the process that needs it:

```powershell
$env:OPENAI_API_KEY = "your-api-key"
dotnet run --project services/Franky.Runtime
```

Optional settings:

- `FRANKY_OPENAI_MODEL` selects the Responses API model.
- `FRANKY_OPENAI_BASE_URL` overrides the API root for testing or a compatible provider.

The previous `ASSISTANT_OPENAI_MODEL` and `ASSISTANT_OPENAI_BASE_URL` names remain accepted as compatibility fallbacks.

Do not commit a key, paste it into a prompt, or write it into project configuration.

## Conversation state and privacy

The first implementation sends `store: true` and continues a conversation with `previous_response_id`. OpenAI currently documents at least 30 days of application-state retention for stored Responses API data in its [data controls guide](https://platform.openai.com/docs/models/default-usage-policies-by-endpoint). Treat household conversation as data leaving the local network, review the current policy before real use, and avoid sensitive conversations during development.

Before calling the cloud-ready voice path complete, make retention behavior a visible configuration decision and add an explicit local-only mode. Runtime diagnostics intentionally exclude prompt and response text by default.
