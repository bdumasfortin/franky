# Spoken Loop Roadmap

Status: **Approved sequence; implementation in progress**

## Scope

This roadmap records the next seven product and system increments approved on
August 27, 2026. Work stops before shared lists and media integrations. Those
features can be reconsidered after Franky owns a dependable, headless spoken
loop.

The sequence is deliberately end-to-end: first prove what already exists, then
make Franky speak, make that speech controllable and conversational, and only
then move device ownership into the runtime.

## Approved sequence

### 1. Verify the existing physical voice path

- Exercise both allowlisted read-only diagnostics through “Yo Franky”.
- Exercise “How is it going?” through the deterministic device-action route.
- Observe audible playback, truthful success reporting, and stage latency while
  Whisper and Ollama share the computer.
- Keep recordings and transcripts ephemeral.

This remains a manual evidence gate. Code, logs, or protocol acknowledgements
alone do not establish that the room-facing experience worked.

### 2. Add a local speech-synthesis boundary and choose Franky's voice

- Introduce a replaceable local synthesizer interface and deterministic tests.
- Evaluate a small number of local engines and voices for licensing, privacy,
  intelligibility, latency, resource use, and personality fit.
- Require explicit approval before selecting the production engine and voice.
- Normalize output to bounded 16 kHz, 16-bit, mono PCM for the current board
  path unless measured evidence justifies another negotiated format.

### 3. Define acknowledged and cancellable response-audio messages

- Give every response and audio segment an opaque correlation identifier.
- Define start/readiness, bounded audio transfer, accepted, started, completed,
  cancelled, and failed outcomes.
- Make unsupported formats, oversized payloads, late messages, disconnects,
  capture/playback conflicts, and duplicate identifiers explicit failures.
- Use the same semantic lifecycle over USB and the later WebSocket transport,
  even when their wire framing differs.

The exact USB framing and buffering strategy is still an architecture decision.
It must be reviewed before firmware and host implementations make it costly to
change.

### 4. Play generated replies through the board over USB

- Synthesize the final assistant response locally.
- Transfer it through the current development path with bounded memory and
  backpressure.
- Pause wake capture during initial half-duplex playback.
- Report success only after the board confirms completion.
- Recover truthfully from cancellation, timeout, playback failure, or device
  disconnect.

### 5. Add semantic earcons and a universal stop operation

- Establish a small, consistent sound vocabulary for heard, accepted,
  completed, denied, offline, and failed events.
- Route one semantic stop operation to active capture, model work, synthesis,
  queued audio, and playback where cancellation is supported.
- Keep full acoustic barge-in out of this increment; reliable cooperative stop
  is the prerequisite.

### 6. Improve conversational delivery

- Add repair prompts for ambiguous or failed recognition.
- Add a bounded, clearly signalled follow-up listening window.
- Add concise, normal, and detailed response modes.
- Begin speech sentence-by-sentence when safe, with a bounded queue and
  cancellation between segments.
- Define a consistent Franky personality without letting style obscure action
  truth, privacy boundaries, or failures.

### 7. Move device ownership into the runtime

- Introduce a runtime-owned `DeviceSession` as the single source of truth for
  connection, device capabilities, capture, playback, cancellation, and state.
- Finish the shared transport-independent protocol and a simulator/conformance
  suite before adding the production Wi-Fi path.
- Implement authenticated Wi-Fi/WebSocket transport, reconnect, resync, and
  headless startup behavior.
- Add a physical privacy mode that disables microphone capture at the device
  boundary and has an unmistakable device-visible state.
- Make browser surfaces read-only consumers of runtime events rather than
  owners of device/session state.

## Separate passive-display track

A separate Codex task owns exploration of a new, passive control web page. It
will be mocked first and will consume a read-only event contract until the
runtime owns device/session state. The page may display voice transcription,
Franky's response, and a brief description of current activity, but it must not
issue device or assistant actions.

The visual direction is intentionally open. Prior suggestions were not
approved; the separate track requires research, multiple concepts, and
iteration before implementation direction is chosen.

## Current implementation slice

Work has started on the reversible portion of step 2: a provider-neutral local
speech-synthesis boundary, output validation, cancellation, structured
diagnostics, and deterministic tests. This does not select or ship a TTS engine
or voice.

The next architecture gate is the response-audio transport contract. The next
product gate is the local voice bake-off. The next physical gate is the manual
voice-path verification in step 1.

## Evidence rules

- **Built** means the implementation compiles.
- **Automated test** means deterministic checks pass without the physical board.
- **Protocol-tested** means both ends exchange the expected messages, but does
  not imply sound was audible.
- **Manually observed** is required for audible playback, LED presentation,
  wake reliability, and physical privacy behavior.
- **Planned** and **proposed** items must not be described as working.
