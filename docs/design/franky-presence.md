# Franky Presence Design Direction

Status: **Phase Stitch implemented as a passive control-board page**

Franky Presence is the passive page intended to remain open most of the time.
It is not a dashboard, transcript archive, or replacement for the engineering
control board. Its job is to make the current relationship between person,
assistant, and trusted system activity legible without demanding attention.
The product-facing name **Franky Presence** remains provisional.

## Concise design brief

Create a dark, centered, room-readable display that feels like a precise local
instrument. Make Franky's reply the largest element, keep the latest transcript
secondary, and communicate lifecycle through sparse type, color, and the
cadence of a three-mark “phase stitch.” Exceptional states must interrupt that
quiet language with unmistakable symbols and large text. Content remains
ephemeral and the page remains entirely passive.

## Non-negotiable interaction principles

1. **Truth before spectacle.** Show only runtime-owned lifecycle and capability
   metadata; never fabricate reasoning, progress, or speech synchronization.
2. **Privacy overrides personality.** Microphone-off suppresses all ordinary
   content and motion. Offline and error also invalidate stale turn content.
3. **One current turn, not history.** Preserve the latest useful transcript and
   reply only while the authoritative event says they remain relevant.
4. **Presence stays peripheral until meaning changes.** Idle motion is minimal;
   listening, work, acting, and speaking use distinct but restrained cadence.
5. **Words carry the state.** Color and motion reinforce meaning but never bear
   it alone. Exceptional states add a simple symbol and large direct language.
6. **Reduced motion is semantically equivalent.** Removing animation must not
   remove state, hierarchy, content, or privacy cues.
7. **No controls on the surface.** Simulation belongs in a separate development
   harness. The production page remains a one-way display.

## Research synthesis

### Evidence

- Calm-technology literature argues for interfaces that move between the
  center and periphery of attention instead of constantly demanding focus.
  That supports a quiet idle state and stronger changes only when lifecycle or
  safety meaning changes. Sources: [Designing Calm Technology](https://calmtech.com/papers/designing-calm-technology)
  and [Ambient Displays: Turning Architectural Space into an Interface between People and Digital Information](https://doi.org/10.1145/642611.642642).
- Commercial voice lifecycle guidance separates listening, thinking, speaking,
  privacy, and failure rather than collapsing them into a generic “active”
  animation. Source: [Amazon Alexa invocation and lifecycle guidance](https://developer.amazon.com/en-US/docs/alexa/alexa-auto/invoking-alexa.html).
- Accessibility guidance requires status changes to be programmatically
  available without moving focus, supports user preference for reduced motion,
  and treats continuous motion as something users ordinarily need to pause.
  Sources: [WCAG status messages](https://www.w3.org/WAI/WCAG22/Understanding/status-messages.html),
  [Media Queries: `prefers-reduced-motion`](https://www.w3.org/TR/mediaqueries-5/#prefers-reduced-motion),
  and [WCAG pause, stop, hide](https://www.w3.org/WAI/WCAG22/Understanding/pause-stop-hide.html).
- Across-room interfaces need short language, strong scale differences, and
  conservative line length. Sources: [Alexa Presentation Language style guide](https://developer.amazon.com/en-US/alexa/alexa-haus/apl-style-guide)
  and [Apple typography guidance](https://developer.apple.com/design/human-interface-guidelines/typography).

### Inspiration

- [Solari's split-flap departure board](https://www.moma.org/collection/works/91954)
  contributed the idea of discrete, purposeful state changes without importing
  its noise or mechanical nostalgia.
- [Teenage Engineering OP-1](https://teenage.engineering/products/op-1/original)
  and [Monome norns](https://monome.org/docs/norns/play/) informed the local-
  instrument quality: economical signals, direct state, and controlled character.
- [Muriel Cooper's information design](https://www.media.mit.edu/posts/muriel-cooper-lasting-imprint/)
  and [Channel 4's identity system](https://www.pentagram.com/work/channel-4)
  suggested that type and modular marks can carry identity without a mascot.
- [Jenny Holzer's LED works](https://projects.jennyholzer.com/LEDs/for-7-world-trade-2006/gallery)
  reinforced the expressive value of language, scale, pacing, and absence.

### Inference

The combination of calm-display research, lifecycle separation, and long-range
typography implies that Franky's primary surface should use very few persistent
elements. A small but recognizable motion signature can maintain presence at
idle, while transcript, reply, and activity appear only when they answer a
current question about what Franky heard, said, or is truthfully doing.

### Design judgment

The approved Phase Stitch direction uses three narrow vertical marks instead of
an orb, face, waveform, or horizon. The center mark changes cadence by phase;
green communicates open input, ember communicates computation or action, and
neutral white supports speech and rest. This is a project-specific identity
choice, not a research finding. The palette avoids generic voice-assistant blue
and the mark is deliberately too abstract to become a mascot.

## Explored directions and review outcome

The first review compared three materially different systems: a disciplined
dispatch board, a typographic editorial relay, and a studio-signal interface.
The second and third reviews expanded these into eight dark, centered studies
with different type, cadence, line, and instrument metaphors. The fourth review
tested four deliberate hybrids. The final stress review exercised the chosen
Phase Stitch at full scale across ordinary, privacy, offline, error, narrow,
long-copy, and reduced-motion conditions.

Explicit review decisions were:

- Select **Phase Stitch** for the passive implementation.
- Keep the reply as the dominant element and remove small redundant phase labels.
- Use phase color and cadence as supporting cues.
- Keep symbols, distinct palettes, and large state text for privacy, offline,
  and error.
- Stage long spoken replies into deterministic phrases while retaining the full
  accessible text.
- Use a dark surface; a light alternative was not selected.

## Known tension

The approved idle state moves continuously at a very slow cadence and the page
has no controls by design. Reduced-motion preference makes the state static,
but this does not strictly resolve WCAG 2.2 Success Criterion 2.2.2 for users
who do not set that preference. This is a conscious product tension to revisit
before calling the page production-ready.

The control-board tab now drives the page with truthful USB-session lifecycle
events and a 3.5-second disconnect grace period. The separate harness retains
the deterministic stress states. Activity wording, ephemeral retention policy,
and the eventual transport remain provisional until the runtime owns complete
device and session truth.
