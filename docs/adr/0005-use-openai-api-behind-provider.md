# ADR-0005: Use the OpenAI API behind a replaceable conversation provider

- Status: Superseded as the current provider by ADR-0009; retained as the optional OpenAI adapter decision
- Date: 2026-08-18
- Deciders: Project owner, engineering recommendation

## Context

V1 should demonstrate an open-ended conversation with a ChatGPT-class model. The project owner has a ChatGPT subscription, but application access requires an OpenAI API key and API-platform billing or credits. A ChatGPT subscription is not used as an application credential.

The official API quickstart requires an API key and directs developers to API billing. The runtime must not embed or log this key.

## Decision

- Implement a replaceable conversation-provider interface.
- Implement the first provider with the OpenAI Responses API.
- Read `OPENAI_API_KEY` from the process environment.
- Default to a configurable low-latency/cost model rather than a model used internally by the ChatGPT product.
- Use API-managed response continuation for v1 and disclose the associated cloud-data boundary.
- Provide a local demo provider so development and tests do not require API credentials.

## Consequences

### Positive

- V1 can demonstrate open-ended conversation and function calling.
- The provider boundary can later host local or other model runtimes.
- Tests can use deterministic fake providers.

### Negative

- A separate API account/billing setup is required for the real integration.
- Prompts and responses cross the local network boundary and are subject to API data controls.
- Model behavior, cost, and latency require evaluation.

### Follow-up

- Create a project-scoped API key outside the repository.
- Add a small evaluation set before changing the default model.
- Decide whether later voice uses separate STT/TTS providers or a realtime audio model.

## References

- [OpenAI API developer quickstart](https://developers.openai.com/api/docs/quickstart)
- [OpenAI API authentication](https://developers.openai.com/api/reference/overview#authentication)
- [OpenAI API data controls](https://platform.openai.com/docs/models/default-usage-policies-by-endpoint)
