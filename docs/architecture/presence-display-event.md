# Provisional Presence Display Event

Status: **Provisional event contract; implemented for the browser-owned USB path**

This contract describes the one-way data Franky Presence needs from the active
Franky session. It is not an accepted transport architecture. The current USB
implementation receives live events from the control-board tab over an
ephemeral, same-origin `BroadcastChannel`; the development harness supplies the
same event shape from deterministic local mocks.

The eventual owner should be the runtime component that has authoritative
device and session lifecycle state. Server-Sent Events are the likely transport
because the display needs a one-way feed, but transport selection waits until
that ownership exists.

## Event shape

```json
{
  "version": 1,
  "sequence": 42,
  "turnId": "turn-0042",
  "phase": "acting",
  "transcript": "Is the player available?",
  "reply": null,
  "activity": "Checking the player",
  "privacy": "available",
  "occurredAt": "2026-08-28T00:42:42.000Z"
}
```

| Field | Type | Meaning |
| --- | --- | --- |
| `version` | integer | Contract version. The consumer accepts only `1`. |
| `sequence` | non-negative integer | Runtime-owned ordering value. It increases across connection and turn events. |
| `turnId` | string or `null` | Correlates phases belonging to one utterance without creating display history. |
| `phase` | enum | `offline`, `idle`, `listening`, `transcribing`, `processing`, `acting`, `speaking`, or `error`. |
| `transcript` | string or `null` | Latest completed user transcription when it is safe and relevant to display. |
| `reply` | string or `null` | Latest Franky response when it is safe and relevant to display. |
| `activity` | string or `null` | Short, trusted lifecycle or tool description supplied by the runtime. |
| `privacy` | enum | `available`, `muted`, or `unknown`. `unknown` is valid only with `offline` or `error`. |
| `occurredAt` | ISO-8601 string | When the authoritative state change occurred. It is not used as a progress estimate. |

## Phase semantics

| Phase | Runtime meaning | Typical display |
| --- | --- | --- |
| `offline` | The display feed or authoritative Franky session is unavailable. | Offline state only; previous turn content is suppressed. |
| `idle` | Franky is available and not handling a current utterance. | Most recent transcript and reply, if still within the runtime's ephemeral retention policy. |
| `listening` | The active endpoint is accepting the user's utterance. | Listening state; no invented transcript. |
| `transcribing` | Captured speech is being converted to text. | Transcribing state; no partial text unless a later contract explicitly supports it. |
| `processing` | Franky is interpreting the completed transcript or preparing a response. | Transcript plus a trusted high-level activity, if present. |
| `acting` | A named allowlisted capability is executing or its truthful result is pending. | Transcript plus runtime/tool-owned activity such as “Checking the player.” |
| `speaking` | Response audio is being produced or played. | Transcript and reply. Visual pacing is not evidence of audio progress. |
| `error` | The current session or turn cannot continue normally. | Specific user-safe activity, without stale turn content. |

## Consumer rules

1. Render exactly one latest accepted event. Do not accumulate history.
2. Reject malformed events, unsupported versions, and sequence values less than
   or equal to the last accepted sequence.
3. Treat `privacy: "muted"` as a presentation override. Suppress transcript,
   reply, activity, and ordinary phase cues, and show only the microphone-off state.
4. Reject `privacy: "unknown"` for ordinary phases so uncertain microphone
   truth can never expose transcript or reply content.
5. Treat `offline` and `error` as authoritative invalidations of stale turn
   content even if those fields are accidentally present.
6. Display `activity` only when it comes from trusted runtime lifecycle or
   allowlisted capability metadata. Never generate it from hidden model
   reasoning, model chain-of-thought, or a guessed percentage complete.
7. Do not infer completion from assistant text. Acting completes only when the
   capability or device returns its structured outcome.
8. Phrase staging is a deterministic display transformation. It never changes
   event meaning and never claims synchronization with synthesized audio.
9. On a feed disconnect, enter offline after a bounded grace period. The USB
   browser feed currently uses 3.5 seconds. On reconnect, require a new
   authoritative snapshot before showing idle or turn content.

## Security and privacy boundary

The passive surface remains a one-way event consumer. Its current channel is
available only to same-origin browser contexts served by the loopback control
board. The intended future runtime route must be read-only and loopback or
explicitly authenticated for its deployment context. Franky Presence must not
gain POST endpoints, serial access, tool execution, command controls, retries,
settings, or model prompts.

Transcript and reply fields are ephemeral display material. The page must not
persist them in browser storage, analytics, logs, URLs, or a conversation
archive. Cloud privacy boundaries remain properties of the selected speech or
conversation provider; the display does not create a new one.

## Accessibility semantics

Visual transitions are decorative supplements to the text state. Each accepted
event produces one concise polite status update for assistive technology. The
full reply is announced even when the visual presentation stages a long answer
into phrases. Reduced-motion mode shows an equivalent static state.
