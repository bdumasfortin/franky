# Device Protocol v1

Status: **Draft — physical audio path verified; Wi-Fi transport not implemented**

This is the initial network contract between the custom ESP32 firmware and the computer runtime. It makes the accepted WebSocket, JSON, and raw PCM direction concrete. The working USB prototype now uses local wake detection and natural speech endpointing, so that lifecycle must be added to this contract before the Wi-Fi transport is implemented.

## Session

- The ESP32 initiates one authenticated WebSocket connection to the computer.
- The URL and credential are provisioned during development and are not compiled into public firmware or committed to Git.
- Text frames contain JSON control messages. Binary frames contain audio only.
- Every JSON message includes `version`, `type`, and a monotonically increasing `sequence` number.
- Either side reconnects with bounded backoff after a failed session.

## Initial handshake

Device to computer:

```json
{
  "version": 1,
  "type": "hello",
  "sequence": 1,
  "device_id": "franky-device",
  "capabilities": ["push_to_talk", "pcm_input", "pcm_output", "status_leds"]
}
```

Computer to device:

```json
{
  "version": 1,
  "type": "hello_ack",
  "sequence": 1,
  "session_id": "opaque-session-id",
  "audio": {
    "encoding": "pcm_s16le",
    "sample_rate_hz": 16000,
    "channels": 1
  }
}
```

The 16 kHz sample rate and signed 16-bit samples are verified on the board's USB development path. They remain subject to negotiation on the network path. Both peers reject an unsupported format before audio begins.

## Push-to-talk input

This flow is the accepted transport baseline from [ADR-0004](../adr/0004-use-websocket-json-and-pcm.md). It has not been implemented over Wi-Fi.

1. The ESP32 sends an `audio_start` JSON frame when the user presses the talk control.
2. The ESP32 streams short binary PCM frames while the control remains active.
3. The ESP32 sends `audio_stop` after release.
4. The computer acknowledges the utterance and moves through `thinking`, `speaking`, and `idle` states.
5. The ESP32 does not capture microphone audio while it plays the first response implementation.

Each utterance has an opaque `utterance_id`. Control messages carry that identifier; binary frames belong to the currently active utterance. Multiple simultaneous utterances are invalid in v1.

## Wake-driven input extension

The working USB prototype detects a wake phrase locally, plays an
acknowledgement, keeps a short pre-roll, captures until trailing silence, and
sends one bounded mono utterance to the computer. The current custom build uses
**“Yo Franky”** through microWakeWord; **“Hi ESP”** WakeNet remains a fallback.
Before network implementation, this document must define the corresponding wake
event, utterance metadata, size limits, acknowledgements, cancellation behavior,
and retry policy.

The USB development protocol is version 4 and reports its active configuration
after each `READY` line:

```text
READY FRANKY_DEVICE 4 16000 2 16 30.0
WAKE_ENGINE microwakeword yo_franky
```

Fallback firmware reports `WAKE_ENGINE wakenet9 hi_esp`. A detection uses the
same phrase identifier, for example `WAKE yo_franky`. The browser must render
the reported engine and phrase rather than assuming one.

## Assistant state

The computer sends a `state` message with one of:

- `idle`
- `listening`
- `thinking`
- `speaking`
- `error`

Firmware owns the exact LED presentation. The protocol communicates semantic state rather than colors or animation timing.

## Failure behavior

- Invalid JSON, sequence regressions, unsupported versions, and audio outside an active utterance are protocol errors.
- The computer stops accepting audio when its bounded input buffer is full and reports an error rather than growing memory without limit.
- Authentication failure closes the connection without returning secret-bearing diagnostics.
- Heartbeat intervals, frame duration, buffer bounds, and reconnect limits remain measurement-driven decisions for the hardware-arrival increment.
