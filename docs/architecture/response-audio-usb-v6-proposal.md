# USB Response Audio v6 Proposal

Status: **Proposed; explicit architecture approval required**

## Decision to make

Choose the first safe host-to-board audio transfer for generated Franky
responses over the current USB development path.

This proposal does not change the accepted WebSocket/JSON/PCM network direction
and does not claim that response playback is implemented.

## Recommendation

Use a fully buffered, acknowledged PCM upload for the first proof:

- USB protocol version 6;
- exactly 16 kHz, signed 16-bit, mono, little-endian PCM;
- one active audio or capture operation at a time;
- at most 512 KiB per response segment, or 16.384 seconds;
- PSRAM-only response allocation;
- raw chunks of at most 8 KiB with an acknowledgement after each chunk;
- exact offsets and CRC32 before playback;
- playback on a worker task so the command loop can accept `STOP`, `PING`,
  `INFO`, and `BYE`;
- success only after the codec finishes and the board returns `PLAY_DONE`.

Full buffering adds wait time before speech begins, but it avoids USB-driven
speaker underruns and keeps the first playback state machine inspectable.
Sentence-by-sentence synthesis can later keep each segment short. Real-time
ring-buffer streaming remains a later optimization backed by measurements.

## Proposed messages

The host begins one operation:

```text
PLAY_BEGIN <id> <byte_count> 16000 1 16 <crc32_hex>
```

The board validates the request, pauses wake detection, reserves PSRAM, and
advertises its chunk bound:

```text
PLAY_READY <id> 8192
```

The host sends sequential, acknowledged chunks:

```text
PLAY_CHUNK <id> <offset> <length>
<exactly length raw bytes>
```

```text
PLAY_RECEIVED <id> <next_offset>
```

After every byte is acknowledged, the host commits the response:

```text
PLAY_COMMIT <id>
```

The board verifies the declared length and CRC before playback:

```text
PLAY_START <id>
PLAY_DONE <id>
```

Terminal alternatives are operation-scoped:

```text
PLAY_FAILED <id> <reason>
STOPPED playback <id>
```

The identifier is a nonzero unsigned 32-bit integer. UUID parsing provides no
benefit while the protocol permits only one operation.

## Bounds and rejection behavior

- `byte_count` must be nonzero, even, and no greater than 524,288.
- `offset` must exactly match the next expected byte.
- `length` must be nonzero, even, at most 8,192, and stay within the declared
  total.
- Unsupported formats, duplicate or stale identifiers, unexpected offsets,
  excessive lengths, allocation failure, receive timeout, CRC mismatch, and
  codec failure terminate the operation with a specific reason.
- The raw chunk has no delimiter. Firmware consumes exactly the declared bytes
  before returning to line parsing.
- CRC32 detects corruption and framing loss; it is not authentication.
- A failed or partial binary write requires bounded firmware recovery and a
  host reconnect. If the current console input cannot time out and resynchronize
  reliably, mixed line/raw framing is blocked and a self-synchronizing framing
  design such as COBS must replace it.

## Required board activity model

Replace loosely related activity booleans with one owner and one active state:

```text
idle
manual_capture
wake_capture
response_upload
response_playback
sfx_playback
```

The activity owner controls transitions, wake pause/resume, response buffers,
LED state, and the single terminal acknowledgement. The playback mutex remains
useful for the codec but cannot represent whole-device activity.

While busy, the command loop accepts only `PING`, `INFO`, `STOP`, `BYE`, and the
expected upload messages. Other operations fail as busy without changing state.

Firmware version 6 should advertise capabilities rather than relying only on a
version number:

```text
CAPABILITIES wake_capture response_pcm16le stop_all
```

The host refuses generated playback when the capability is absent.

## Universal stop behavior

`STOP` becomes idempotent and applies to the active operation:

- manual capture ends early and returns the bounded clip;
- wake capture is cancelled and discarded;
- response upload is discarded;
- response and named-SFX playback stop at the next codec chunk;
- idle returns `IDLE`.

```text
STOPPING <activity> <id-or-0>
STOPPED <activity> <id-or-0>
```

The existing 256-sample codec writes provide a target cancellation check roughly
every 16 ms. Cancellation must mute and drain output, restore volume, free owned
memory, resume wake detection exactly once, and return the correct idle/offline
state.

An in-band stop cannot interrupt a raw chunk body. The host finishes the current
chunk before sending `STOP`; the 8 KiB chunk bound limits that delay. All serial
writes, including heartbeats, must use one ordered queue so no command can land
between a chunk header and body.

## Alternatives

| Alternative | Benefit | Cost or risk |
| --- | --- | --- |
| Recommended fully buffered, acknowledged chunks | Smallest debuggable slice; no playback underrun tuning | Speech begins after the segment upload; bounded PSRAM use |
| Live ring-buffer streaming | Lowest possible start latency | Backpressure, underrun, cancellation, and producer/consumer races arrive at once |
| One header followed by one entire raw body | Simpler happy path | Long uninterruptible receive window and poor recovery after a partial write |
| Self-synchronizing binary frames such as COBS | Strong recovery from truncation and delimiter bytes | Larger parser and host implementation; justified if console raw-body recovery fails |

## Blockers before implementation

1. Prove a partial raw chunk can time out and the firmware can resynchronize; if
   not, select self-synchronizing framing.
2. Move SFX and response playback off the firmware command loop.
3. Introduce one board-wide activity owner before adding upload flags.
4. Serialize browser writes so heartbeat and playback bytes cannot interleave.
5. Gate playback on advertised capability and matching operation identifiers.
6. Measure USB throughput and a 512 KiB PSRAM allocation while the wake model and
   audio front end remain initialized.

## Validation

Automated parser and state tests must cover successful one/multi-chunk transfers,
every bound, wrong offsets, stale identifiers, CRC failure, truncated chunks,
stop in every activity, conflicting commands, disconnects, reconnect, command-
looking binary data, codec failure, cleanup, and exactly one terminal outcome.

Physical checks must establish audible output, bounded stop behavior, no wake
detection during playback, correct LED state, no looping, and recovery after
unplugging during upload and playback.
