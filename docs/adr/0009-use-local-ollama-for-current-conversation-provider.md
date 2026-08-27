# ADR-0009: Use local Ollama for the current conversation provider

- Status: Accepted
- Date: 2026-08-27
- Deciders: Project owner, engineering recommendation
- Supersedes: ADR-0005's current provider selection, while preserving its provider boundary and optional OpenAI adapter

## Context

Franky's OpenAI Responses adapter requires an OpenAI Platform API key and
separate usage billing. A ChatGPT Pro or Codex subscription is not an
application credential. The project owner selected a local provider for now
and explicitly wants an easy cloud-provider switch later.

The development computer has an NVIDIA RTX 3070 Ti with 8 GB of VRAM and 16 GB
of system memory. It already runs Whisper locally. Ollama supports Windows,
NVIDIA acceleration, and function calling. `qwen3.5:4b` is a 3.4 GB quantized,
tool-capable package that leaves more headroom for Whisper than the 8B option.

## Decision

- Make Ollama the control-board launcher's default conversation provider.
- Start with `qwen3.5:4b` and disable model thinking for lower command latency.
- Use Ollama's native chat API and retain conversation messages in Franky's
  in-memory session.
- Preload the selected model when Franky starts and refresh a configurable
  keep-alive duration on each request so the first spoken command does not pay
  the full cold-load cost.
- Keep provider selection explicit through `FRANKY_ASSISTANT_PROVIDER`.
- Preserve the OpenAI Responses adapter as an optional cloud provider.
- Make tool definitions provider-neutral, then map them to each provider's
  request shape without changing the strict named-command executor.
- Keep deterministic demo mode as the no-model fallback.

## Consequences

### Positive

- Normal use needs no model API key or metered inference account.
- Transcript text and conversation remain local when Ollama is selected.
- The provider boundary is now exercised by two real implementations.
- A later cloud adapter can be added without changing speech capture, wake
  handling, capabilities, or the control-board endpoint.

### Negative

- Local model quality is lower and less predictable than a frontier cloud
  model, especially for ambiguous requests.
- Ollama and the model add a multi-gigabyte local installation.
- Whisper and the language model share finite GPU memory; real concurrent
  latency must be observed on the target computer.
- The Ollama session history currently lives only in memory and resets with the
  runtime.

## Validation required

- Ordinary replies and both allowlisted read-only commands are verified through
  the live loopback Ollama service.
- Exercise the complete physical wake-to-command path and observe latency while
  Whisper and Ollama share the GPU.
- Keep unknown and arbitrary commands rejected by the fixed executor.

## References

- [Ollama on Windows](https://docs.ollama.com/windows)
- [Ollama tool calling](https://docs.ollama.com/capabilities/tool-calling)
- [Qwen 3.5 4B package](https://ollama.com/library/qwen3.5:4b)
- [OpenAI developer quickstart](https://developers.openai.com/api/docs/quickstart)
