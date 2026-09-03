# Franky documentation

This documentation separates the system that is working today from the target
Wi-Fi voice-assistant architecture. Accepted architecture decision records are
the authoritative history for consequential choices.

## Start here

Current pause: [September 3 session handoff](development/session-handoff-2026-09-03.md)
records candidate v2's failed practical trial, activation-cue contamination,
the exact local state, and the next investigation.

| Document | What it covers |
| --- | --- |
| [Product definition](product/v1.md) | The approved experience, boundaries, and success criteria |
| [Architecture overview](architecture/overview.md) | Responsibilities of the ESP32 and computer runtime |
| [Implementation plan](plan/v1-implementation.md) | Completed increments, active work, and deferred scope |
| [Spoken-loop roadmap](plan/spoken-loop-roadmap.md) | Approved seven-step sequence and the separate passive-display track |
| [Franky Presence design](design/franky-presence.md) | Research synthesis, design principles, explored directions, and approved visual language |
| [Development workflow](development/workflow.md) | Local setup, validation, secrets, and evidence expectations |
| [Hardware bring-up](../firmware/hardware-bring-up.md) | Evidence collected from the physical Waveshare board |

## Guides

- [Franky control board](../tools/franky-control-board/README.md) — run the
  browser interface and exercise audio, LEDs, wake detection, and transcription.
- [Franky Presence](../tools/franky-presence/README.md) — run the passive live
  display or its deterministic development harness.
- [Firmware](../firmware/README.md) — understand and flash the ESP32-side project.
- [Wake-word training](../tools/wake-word/README.md) — generate the local “Yo
  Franky” model used by custom firmware builds.
- [Computer runtime](../services/README.md) — understand the .NET host boundary.
- [Local Ollama development](development/local-ollama.md) — install and run the
  default local conversation provider or switch providers.
- [Physical voice-path validation](development/physical-voice-path-validation.md)
  — collect the manual evidence required for the current USB slice.
- [Wake-word data collection](development/wake-word-data-collection.md) — use
  the private guided collector and evaluate physical samples before retraining.
- [OpenAI API development](development/openai-api.md) — configure the optional
  cloud conversation provider and understand its privacy boundary.

## Architecture and protocol

- [Architecture overview](architecture/overview.md)
- [Device protocol v1](architecture/device-protocol-v1.md)
- [USB response-audio v6 proposal](architecture/response-audio-usb-v6-proposal.md)
- [Provisional presence display event](architecture/presence-display-event.md)
- [Architecture Decision Records](adr/README.md)

## Product

- [Version 1 product definition](product/v1.md)
- [Naming decision](product/naming.md)

## Plans

- [Version 1 implementation plan](plan/v1-implementation.md)
- [Spoken-loop roadmap](plan/spoken-loop-roadmap.md)
- [Local TTS and voice evaluation](plan/tts-voice-evaluation.md)

## Design

- [Franky Presence design direction](design/franky-presence.md)

## Documentation conventions

- **Working** means built and observed on the current hardware or computer.
- **Planned** means approved direction that has not been implemented yet.
- **Proposed** decisions are not implementation authorization.
- **Accepted** ADRs are historical records; changing an accepted decision
  requires a new superseding ADR rather than silently rewriting the old one.
