# Franky Presence

Status: **Passive control-board page with live USB-session events**

Franky Presence is the passive, always-open display surface for Franky. The
approved **Phase Stitch** direction uses a restrained three-mark signal, large
centered language, and state-specific cadence and color. The control-board
service serves it as a separate page at `/presence/`, while the engineering
controls remain at the root. The product-facing name remains provisional.

The presence page has no commands, buttons, settings, connection controls,
retry actions, media controls, diagnostics, POST requests, serial access, or
tool execution. The open control-board tab publishes ephemeral snapshots over a
same-origin browser channel and does not store transcripts or replies. The
separate harness remains deterministic, local, and read-only.

## Run the live page

Start the normal control-board service:

```powershell
tools/franky-control-board/serve.ps1
```

Then open one of these pages:

- `http://127.0.0.1:8765/presence/` — passive page receiving live snapshots
  from the open control-board tab.
- `http://127.0.0.1:8765/presence/?mock=speaking` — passive page fixed to one
  mock state.
- `http://127.0.0.1:8765/presence/?source=mock` — passive deterministic mock
  lifecycle.
- `http://127.0.0.1:8765/presence/harness.html` — development harness whose controls
  remain outside the embedded presence page.

Fixed mock names are `offline`, `ready`, `idle`, `listening`, `transcribing`,
`processing`, `acting`, `speaking`, `privacy`, `error`, and `long`.

The `source=harness` query mode is an internal development seam. It accepts
same-origin `postMessage` display events and a reduced-motion override from the
separate harness. The default live mode accepts same-origin
`BroadcastChannel` snapshots from the control-board tab and enters offline when
they stop for 3.5 seconds. Neither channel is the future runtime transport.

## Behavior

- Only the latest event is rendered; the page never builds conversation history.
- A connected idle session with no completed turn shows a visible **Ready**
  state instead of an empty dark surface.
- Lower or repeated sequence numbers are discarded.
- Muted privacy state overrides all ordinary phase content.
- Offline, privacy, and error have large text, distinct palettes, and simple
  geometric symbols so color is never their only cue.
- Long spoken replies use deterministic phrase staging. The full reply remains
  available to assistive technology, and reduced-motion mode renders it all at
  once without animation.
- Page motion pauses when the document is hidden.
- The approved visual direction is dark-only. A light theme was not selected.

The subtle idle stitch moves continuously at the approved cadence. This is a
deliberate always-open presence choice, but it does not strictly satisfy WCAG
2.2 Success Criterion 2.2.2 because the passive page has no pause control.
`prefers-reduced-motion: reduce` provides an equivalent static presentation.

The provisional one-way event semantics are documented in
[`docs/architecture/presence-display-event.md`](../../docs/architecture/presence-display-event.md).
The research synthesis and review decisions are in
[`docs/design/franky-presence.md`](../../docs/design/franky-presence.md).

## Not implemented

- No runtime-owned Server-Sent Events or other production transport.
- No runtime-owned device or session state aggregation.
- No speech-timing synchronization; phrase staging is visual pacing only.
- No serial, tool, or capability access from the passive page.
