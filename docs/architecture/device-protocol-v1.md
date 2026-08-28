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

The USB development protocol is version 5 and reports its active configuration
after each `READY` line:

```text
READY FRANKY_DEVICE 5 16000 2 16 30.0
WAKE_ENGINE microwakeword yo_franky
CAPABILITIES wake_capture named_sfx wake_threshold wake_diagnostics wake_sample
WAKE_THRESHOLD 96
WAKE_DIAGNOSTICS OFF
```

Fallback firmware reports `WAKE_ENGINE wakenet9 hi_esp`. A detection uses the
same phrase identifier, for example `WAKE yo_franky`. The browser must render
the reported engine and phrase rather than assuming one.

The custom-model diagnostic build accepts two additive version 5 commands:

```text
WAKE_THRESHOLD <50-to-99>
WAKE_DIAGNOSTICS ON
WAKE_DIAGNOSTICS OFF
```

Threshold changes are percentages, remain in memory only, and reset to 96% on
reboot. Unsupported values and fallback firmware fail explicitly. When enabled,
diagnostics report only smoothed model scores and do not transmit audio:

```text
WAKE_SCORE <peak_percent> <threshold_percent> detected
WAKE_SCORE <peak_percent> <threshold_percent> near_miss
```

The browser enables these controls only when both corresponding capabilities
are advertised. This diagnostic extension does not reserve protocol version 6,
which remains proposed for response audio.

The same custom-model build accepts an explicitly initiated, bounded diagnostic
sample request:

```text
WAKE_SAMPLE <500-to-5000-ms>
```

The board disarms wake detection without stopping the ESP-SR Audio Front End,
reports `WAKE_SAMPLE_START <duration_ms>`, and returns one existing `AUDIO`
binary body containing the exact post-AFE mono stream supplied to custom-model
inference. It restores the wake engine after the body and `END` marker. The
control board currently requests three seconds and never persists the returned
audio without a separate user acceptance action. This additive diagnostic uses
version 5 framing; version 6 remains reserved for host-to-device response audio.

## USB named SFX extension

The version 5 USB protocol accepts one allowlisted playback command:

```text
SFX frankys_suuuper
```

The board responds with `SFX_START frankys_suuuper`, enters the semantic
`speaking` state, plays the embedded PCM, and returns
`SFX_DONE frankys_suuuper` only after the codec write completes. Unsupported
names return `ERROR unknown_sfx`. The browser uses a bounded acknowledgement
timeout and treats disconnects and firmware errors as failures.

## Generated response audio

Generated response playback is not implemented. The proposed USB version 6
framing, acknowledgements, bounds, cancellation behavior, activity ownership,
alternatives, and unresolved recovery proof are documented separately in the
[USB response-audio proposal](response-audio-usb-v6-proposal.md). It must receive
explicit architecture approval before firmware and host implementation.

The stable semantic lifecycle should be shared by USB and the future network
transport: proposed, ready, transferred, started, completed, cancelled, or
failed. USB byte framing is a development transport detail and does not amend
the accepted WebSocket text/binary framing from ADR-0004.

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
