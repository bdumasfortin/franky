# Franky Presence

Status: **Standalone mocked implementation; not connected to the runtime**

Franky Presence is the passive, always-open display surface for Franky. The
approved **Phase Stitch** direction uses a restrained three-mark signal, large
centered language, and state-specific cadence and color. It is deliberately
separate from the engineering controls in `tools/franky-control-board/`.
The product-facing name remains provisional.

The presence page has no commands, buttons, settings, connection controls,
retry actions, media controls, diagnostics, POST requests, serial access, or
tool execution. Its mock source is deterministic, local, read-only, and does
not store transcripts or replies.

## Run the mock

Serve this directory from a loopback-only static server. For example:

```powershell
python -m http.server 5173 --bind 127.0.0.1 --directory tools/franky-presence
```

Then open one of these pages:

- `http://127.0.0.1:5173/` — passive page cycling through deterministic events.
- `http://127.0.0.1:5173/?mock=speaking` — passive page fixed to one mock state.
- `http://127.0.0.1:5173/harness.html` — development harness whose controls
  remain outside the embedded presence page.

Fixed mock names are `offline`, `idle`, `listening`, `transcribing`,
`processing`, `acting`, `speaking`, `privacy`, `error`, and `long`.

The `source=harness` query mode is an internal development seam. It accepts
same-origin `postMessage` display events and a reduced-motion override from the
separate harness. It is not the future runtime transport.

## Behavior

- Only the latest event is rendered; the page never builds conversation history.
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

- No Server-Sent Events or other live runtime feed.
- No runtime-owned device or session state aggregation.
- No speech-timing synchronization; phrase staging is visual pacing only.
- No firmware, serial, tool, or capability integration.
