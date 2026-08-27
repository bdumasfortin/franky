# ADR-0004: Use WebSocket, JSON control messages, and PCM audio

- Status: Accepted
- Date: 2026-08-18
- Deciders: Project owner

## Context

The ESP32 needs one bidirectional connection to the computer for device state, button events, captured audio, response audio, and errors. V1 should optimize for ease of implementation and diagnosis rather than bandwidth minimization.

## Decision

- The ESP32 initiates a persistent authenticated WebSocket connection to the computer.
- Text frames contain versioned JSON control messages.
- Binary frames contain 16 kHz, signed 16-bit little-endian, mono PCM audio unless hardware evidence requires a revision.
- The first hardware interaction is half-duplex and push-to-talk.
- Local wake-word activation and simultaneous capture/playback are later increments.

## Consequences

### Positive

- A single ordered connection is simple to inspect and test.
- Raw PCM avoids codec integration and transcoding during the first hardware milestone.
- Push-to-talk isolates microphone, network, assistant, and speaker failures.

### Negative

- Raw PCM uses more bandwidth than compressed audio.
- A single connection requires explicit framing and reconnect behavior.
- The proposed audio format remains unverified on the real board.

### Follow-up

- Finalize message schemas before writing embedded transport code.
- Add authentication, heartbeats, bounded buffering, and compatibility tests.
- Revisit compression only if measurement shows a need.

