# Version 1 Implementation Plan

Status: **Approved direction; implementation in progress**

## Outcome

Build one safe, understandable loop from a spoken request to a spoken response:

```text
wake → listen → transcribe → understand → act or answer → speak
```

The ESP32 owns the room-facing audio and status experience. The computer owns
speech recognition, conversation, capability policy, integrations, and response
generation.

## Completed foundations

### Computer runtime

- .NET 10 modular-monolith scaffold with explicit internal boundaries.
- Deterministic demo, local Ollama, and optional OpenAI Responses API conversation providers.
- Provider-specific in-memory conversation continuation behind one interface.
- Strictly allowlisted read-only command execution.
- Automated checks for command validation, conversation continuation, and tool results.

### Physical board and control surface

- Factory image backed up before the first write.
- Custom Franky firmware built and flashed with ESP-IDF 5.5.2.
- Stereo microphone capture verified from close range to roughly 12 feet.
- Speaker cues and seven-pixel state LEDs verified.
- Local WakeNet9 **“Hi ESP”** detection verified.
- Reproducible custom **“Yo Franky”** microWakeWord training workspace, quantized
  model, TensorFlow Lite Micro integration, and WakeNet fallback implemented.
- Model-enabled firmware built, flashed, booted, observed idle without watchdog
  faults, and verified through a successful physical spoken test.
- Voice-activity endpointing and bounded mono wake capture verified.
- Local Whisper `small.en` transcription working with NVIDIA GPU acceleration and CPU fallback.
- State-driven browser control board working over USB serial.
- Loopback assistant-turn endpoint connects wake transcripts to the existing
  conversation session and named-command tool loop.
- Structured action outcomes and Franky replies render separately in the
  control board; demo mode is explicitly labeled as unable to select tools.
- Assistant bridge, session continuity, busy-turn rejection, and tool-call
  reporting are locally covered for the OpenAI and Ollama request shapes.
- Live `qwen3.5:4b` selected and executed both read-only diagnostics through the
  loopback endpoint. Background model preloading avoids the observed cold-load
  penalty. Both diagnostics later answered correctly after successful physical
  wakes. Across the four non-empty physical requests, runtime logs recorded
  164–439 ms local transcription and 4–1,046 ms assistant-turn processing;
  end-to-end wake latency remains unmeasured.
- A cleaned, metadata-free 16 kHz mono “SUUUPER” asset is embedded in firmware.
- `SFX frankys_suuuper` has explicit start/completion acknowledgements, and the
  browser maps only the fixed `device.sfx.frankys_suuuper` action to it.
- Both conversation providers describe the natural “How is it going?” intent,
  and the UI waits for board completion rather than treating model selection as
  playback success.
- The image is flashed, a direct serial request returned `SFX_START` and
  `SFX_DONE`, and live `qwen3.5:4b` selected the exact device action for “How is
  it going?” A physical “How's it going?” test then returned text with no action,
  so common short variants now route through a narrow deterministic matcher
  before the model. A later physical voice-path check confirmed correct positive
  diagnostic results, correct negative-control behavior, audible “SUUUPER”
  playback, low perceived latency, and truthful status after successful wakes.
  The same check found the custom wake phrase extremely unreliable, sometimes
  requiring about ten repetitions at roughly 20 inches in a quiet room.
- The design-led Franky Presence track produced an approved Phase Stitch visual
  direction and a passive page served by the control-board app. The current USB
  lifecycle, latest transcript, reply, and confirmed device playback state flow
  ephemerally from the control-board tab. Privacy, offline, error, long-copy,
  narrow-screen, reduced-motion, disconnect, and deterministic harness states
  are implemented without giving the passive page controls or serial access.

## Active vertical slice

Fix wake reliability and activation-cue transcript contamination, then repeat
the implemented bridge and device-action evidence pass. The September 3 v2
trial failed: the user reported roughly one wake in five and cue contamination
in every successful transcript. Work is paused; begin with the
[session handoff](../development/session-handoff-2026-09-03.md).

1. Tune or retrain “Yo Franky” from physical evidence rather than accepting the
   synthetic evaluation as representative. A temporary 50–99% cutoff and
   metadata-only peak-score diagnostic are now built, flashed, and verified at
   the USB command level. A physical 96% versus 87% comparison produced roughly
   three detections per ten attempts at both cutoffs, so threshold reduction
   alone is rejected as the fix. A private, explicit record/review/keep
   collector for the model's exact post-AFE input is now built and flashed,
   with an offline current-model evaluator. The first 30-positive/20-hard-
   negative corpus is complete. At 96%, offline scoring detected 25 positives
   and activated on five hard negatives; from 50–99%, no tested cutoff produced
   useful class separation. A matched-score diagnostic is now built, flashed,
   and protocol-tested. A concurrency race in the first spoken diagnostic was
   corrected by scoring the frozen buffer after capture. The repeated samples
   then matched board/offline scores exactly at 100/100, 100/100, and 91/91.
   Two isolated candidates were trained without consuming the later physical
   acceptance session. Candidate v1 was rejected because it still activated on
   5/20 training-corpus hard negatives at 96%. Candidate v2 scored all 30
   positives at 100% and reduced that count to 2/20, while retaining viable
   separate synthetic/ambient results. The user explicitly authorized flashing
   it for a pragmatic trial before formal fresh-session acceptance. It is now
   left flashed at 96%, but the user's live trial was unacceptable. Investigate
   the continuous audio/inference path versus frozen-clip scoring, and preserve
   the original model as the rollback baseline. No new flash is implied.
2. Measure repeated wakes at a fixed distance and quiet-room condition, then
   observe an idle period for false activations.
3. Repeat both read-only diagnostics, “How is it going?”, and the longer
   negative control after both defects are addressed. Downstream behavior passed
   in the earlier August 27 test, but the latest trial reports contaminated
   transcripts and does not establish a downstream pass.
4. Record stage latency rather than relying only on the current “very low”
   subjective observation.
5. Keep ordinary wake audio and transcripts ephemeral. The separately approved
   wake-dataset workflow persists only deliberately recorded and explicitly
   accepted samples in ignored local storage, with individual and whole-set
   deletion.

## Following increments

The detailed, approved order is maintained in the
[spoken-loop roadmap](spoken-loop-roadmap.md). In summary: finish the physical
evidence pass; add a replaceable local TTS boundary and approve a voice; define
acknowledged, cancellable response audio; play replies over USB; add semantic
earcons and universal stop; improve conversational delivery; then move device
ownership into the runtime and complete authenticated, headless operation.

Implementation stops before shared lists and media integrations. The separate
passive-display track now has an implemented read-only browser feed and does not
own device or assistant actions. Once the runtime owns complete device and
session truth, replace that interim channel with a runtime-owned one-way feed
and accept or supersede the provisional presence-display event contract.

## Validation gates

- Release build, formatting check, and automated runtime tests pass.
- Wake capture ends naturally and does not grow without a hard limit.
- Local transcription remains local and does not persist speech by default.
- A model-requested command can execute only a fixed allowlisted process.
- Unknown commands and arbitrary arguments are rejected.
- API, model, device, and command failures produce truthful state and useful diagnostics.
- The speaker never loops a cue after a state transition or disconnect.
- Wi-Fi behavior remains marked unverified until exercised on the physical board.

## Deferred beyond v1

- Multiple ESP32 satellites.
- Persistent personal memory and multiple users.
- General-purpose autonomous behavior.
- Unrestricted or unconfirmed state-changing commands.
- Production-grade acoustic tuning across different rooms.
