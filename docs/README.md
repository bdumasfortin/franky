# Franky documentation

This documentation separates the system that is working today from the target
Wi-Fi voice-assistant architecture. Accepted architecture decision records are
the authoritative history for consequential choices.

## Start here

| Document | What it covers |
| --- | --- |
| [Product definition](product/v1.md) | The approved experience, boundaries, and success criteria |
| [Architecture overview](architecture/overview.md) | Responsibilities of the ESP32 and computer runtime |
| [Implementation plan](plan/v1-implementation.md) | Completed increments, active work, and deferred scope |
| [Franky Presence design](design/franky-presence.md) | Research synthesis, design principles, explored directions, and approved visual language |
| [Development workflow](development/workflow.md) | Local setup, validation, secrets, and evidence expectations |
| [Hardware bring-up](../firmware/hardware-bring-up.md) | Evidence collected from the physical Waveshare board |

## Guides

- [Franky control board](../tools/franky-control-board/README.md) — run the
  browser interface and exercise audio, LEDs, wake detection, and transcription.
- [Franky Presence](../tools/franky-presence/README.md) — run the separate
  passive display against deterministic local mock events.
- [Firmware](../firmware/README.md) — understand and flash the ESP32-side project.
- [Wake-word training](../tools/wake-word/README.md) — generate the local “Yo
  Franky” model used by custom firmware builds.
- [Computer runtime](../services/README.md) — understand the .NET host boundary.
- [Local Ollama development](development/local-ollama.md) — install and run the
  default local conversation provider or switch providers.
- [OpenAI API development](development/openai-api.md) — configure the optional
  cloud conversation provider and understand its privacy boundary.

## Architecture and protocol

- [Architecture overview](architecture/overview.md)
- [Device protocol v1](architecture/device-protocol-v1.md)
- [Provisional presence display event](architecture/presence-display-event.md)
- [Architecture Decision Records](adr/README.md)

## Product

- [Version 1 product definition](product/v1.md)
- [Naming decision](product/naming.md)

## Design

- [Franky Presence design direction](design/franky-presence.md)

## Documentation conventions

- **Working** means built and observed on the current hardware or computer.
- **Planned** means approved direction that has not been implemented yet.
- **Proposed** decisions are not implementation authorization.
- **Accepted** ADRs are historical records; changing an accepted decision
  requires a new superseding ADR rather than silently rewriting the old one.
