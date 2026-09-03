# Physical Voice-Path Validation

Status: **Fail — wake reliability and activation-cue contamination remain open**

## Latest result — September 3, 2026

After candidate v2 was flashed at 96% and the control board was restored in
real local Ollama mode (`qwen3.5:4b`, tools enabled), the user reported roughly
one successful wake in five attempts. On every successful wake, the activation
beep appeared in the transcript. Both are blocking physical-path defects.

This is a user-observed approximate ratio, not a recorded five-trial experiment.
Exact trial counts, transcript wording, current distance/noise conditions, and
fresh audio were not collected. The source of the cue contamination is not yet
diagnosed. Earlier successful downstream observations must not be generalized
to this trial. Candidate v2 is not accepted; no fix or rollback was made before
the user requested a pause. Resume from the
[session handoff](session-handoff-2026-09-03.md).

## Earlier result — August 27, 2026

Gate result: **Fail on wake reliability; downstream voice path passed when the
wake succeeded.**

The user tested at roughly 20 inches from the microphones in a very quiet room.
“Yo Franky” was extremely unreliable and sometimes required about ten
repetitions. Once a wake succeeded:

- every tested transcription was judged correct;
- both positive read-only commands answered correctly;
- the negative control behaved as expected;
- the “SUUUPER” clip was audible;
- latency was judged very low;
- there was no duplicate action, timeout, or misleading status.

The number of successful trials was not recorded, so this evidence does not
establish a pass rate. Runtime logs for the four non-empty physical requests
showed 164–439 ms local transcription processing and 4–1,046 ms assistant-turn
processing. These are stage timings rather than end-to-end wake latency. The
downstream results are manually observed evidence, while the wake miss rate
blocks this gate until sensitivity is improved and retested.

## Diagnostic retest

The diagnostic firmware is flashed and directly verified. Its sensitivity
controls are temporary and its score reports contain no audio.

1. Reload the control board, reconnect Franky, and open **Wake**.
2. Enable **Show near misses** and leave **Temporary cutoff** at 96%.
3. From the same roughly 20-inch position, say “Yo Franky” ten times with a
   consistent pause between attempts. Record every peak and whether it detected.
4. Select 87% and repeat the same ten attempts.
5. Speak several ordinary phrases that do not contain the wake phrase and note
   their reported peaks or any false activation.
6. Leave the board listening during ordinary quiet-room activity and record any
   false activation. Do not describe an unobserved period as evidence.
7. Reboot or return the cutoff to 96% when the comparison ends.

If genuine attempts peak below 50%, threshold tuning cannot solve the mismatch
within the bounded diagnostic range and the model needs representative positive
examples. If wake and non-wake peaks overlap materially, lowering the cutoff is
also not a safe solution; retraining with real positives and hard negatives is
the next step.

### Diagnostic result — August 27, 2026

At the 96% cutoff, the user reported three detections scoring 99%, 99%, and 98%,
one scored near miss at 70%, and seven attempts with no new reported peak. The
submitted sequence contains eleven entries; if one was an accidental duplicate,
the observed result is three detections in ten attempts, otherwise it is three
in eleven.

At the 87% cutoff, the user reported three detections scoring 95%, 97%, and 89%,
scored misses at 73%, 49%, and 55%, and four attempts with no new reported peak.
That is three detections in ten attempts.

A missing report means the diagnostic candidate either never reached its 20%
reporting floor or did not fall below the 10% reset point before the next
attempt; it is not a measured zero. Even with that ambiguity, the detection
count did not materially improve at 87%, and genuine attempts varied from below
the reporting floor to 99%. Lowering the cutoff alone is therefore rejected as
the production fix. Return the board to 96% and use representative physical
positive samples for offline evaluation and retraining.

### Collection and offline evaluation — August 27, 2026

The approved private collector is implemented, built, and flashed. The board
advertises `wake_sample` and a direct one-second request returned exactly 32,000
bytes of 16 kHz mono post-AFE audio. The control board now guides 30 positive
and 20 hard-negative samples through a record, review, and explicit-keep flow.
The user completed that corpus through the UI. All 50 WAV/metadata pairs are
present, hash-valid, and unique.

The deployed model's offline evaluator detected 25/30 positives at the current
96% threshold and activated on 5/20 hard negatives. At 50%, it detected 28/30
positives but activated on 7/20 hard negatives; at 99%, it detected only 21/30
while still activating on 4/20. Five hard negatives scored 97–100%, including
“You’re frankly mistaken” and “Yo friendly people.” The current model therefore
has no usable tested cutoff. These are offline same-session clip results, not a
false-activation-per-hour measurement. Follow the
[wake-word collection guide](wake-word-data-collection.md) for the complete
table and the next gate.

The matched-score diagnostic reports an embedded model peak for every deliberate
`WAKE_SAMPLE` capture while returning those exact same bytes. It was built and
flashed on September 3, then a memory-only 0.5-second protocol probe reported a
3% peak, returned all declared 16,000 audio bytes, emitted `END`, and remained
responsive at the 96% default. No audio was persisted. This verifies framing and
recovery but did not by itself establish board/offline parity.

The first spoken diagnostic set exposed a concurrency race in the diagnostic
scorer, so those board measurements were marked invalid and excluded without
deleting their private WAVs. After changing firmware to score the frozen capture
after collection, the user repeated two “Yo Franky” samples and one “Yo friendly
people” hard negative. Board/offline peaks matched exactly at 100/100, 100/100,
and 91/91. The offline evaluator is therefore validated for the current deployed
pipeline.

Two physically tuned candidates were trained in isolated ignored directories;
neither changed the firmware model during training. Candidate v1 recovered every training-corpus
positive but still activated on 5/20 hard negatives at 96%, so it was rejected.
Candidate v2 scored every positive at 100% and reduced activations to 2/20 at
96%. Its untouched synthetic/ambient test measured 2.0% false rejection and
0.187 estimated false accepts per hour at the reported 95% point, and 3.33%
false rejection with no observed false accepts at 98%. Candidate v2 remains
provisional because all physical figures came from its training corpus. At the
user's explicit request, it was subsequently built and flashed for a pragmatic
trial before formal fresh-session acceptance. The board booted and the control
board observed microWakeWord armed at the 96% default. The subsequent September
3 trial above found poor live wake reliability and cue contamination despite
the favorable reused-corpus scores.

## Purpose

Verify the existing room-facing path before generated speech changes it. Serial
messages and automated tests provide supporting evidence, but they do not prove
that a person heard the expected result or that the device presentation was
truthful.

Run every utterance three times from an ordinary speaking position. Preserve
failed and ambiguous trials rather than averaging them away.

## Test context

- Date and time:
- Git commit:
- Firmware build or hash:
- Transport: USB serial
- Conversation provider/model: Ollama / `qwen3.5:4b`
- Transcriber/model: Whisper / `small.en`
- Speaker distance and orientation:
- Room and noise conditions:
- Ollama state: cold / preloaded
- Audio and transcripts remained ephemeral: yes / no / unverified

## Required interactions

| Exact spoken interaction | Expected route and outcome |
| --- | --- |
| “Yo Franky.” Then: “What version of dot net are you running?” | Wake and bounded capture complete; transcript preserves intent; `runtime.dotnet_version` is selected and completed; the installed SDK version is returned. |
| “Yo Franky.” Then: “Which user account are you running as?” | Wake and bounded capture complete; transcript preserves intent; `system.identity` is selected and completed; the actual OS identity is returned. |
| “Yo Franky.” Then: “How is it going?” | The deterministic route selects `device.sfx.frankys_suuuper`; the clip is audible; the UI does not report success before `SFX_DONE`. |
| “Yo Franky.” Then: “How is it going with the build?” | Negative control: the narrow local matcher does not trigger the “SUUUPER” action merely because the opening words are similar. |

## Trial record

Copy this table for every trial.

| Observation | Result |
| --- | --- |
| Wake phrase detected | pass / fail |
| Wake acknowledgement audible | pass / fail / not observed |
| Listening ended naturally | pass / fail |
| Transcript preserved the intended request | pass / fail |
| Expected action selected | pass / fail / not applicable |
| Action actually completed | pass / fail / not applicable |
| SFX output audible and intelligible | pass / fail / not applicable |
| Reported outcome matched physical reality | pass / fail |
| Unexpected duplicate action or playback | yes / no |
| Wake-to-acknowledgement latency | ___ ms |
| End-of-speech to transcript latency | ___ ms |
| End-of-speech to action start or reply latency | ___ ms |
| Action or playback completion latency | ___ ms |
| Exact anomaly notes | ___ |

## Evidence rules

- Mark sound, LEDs, wake behavior, and physical completion as **manually
  observed** only when personally witnessed.
- Mark a message exchange without the corresponding physical observation as
  **protocol-tested**.
- A correct text reply without an expected action is a failed action route.
- `SFX_START` or `SFX_DONE` without audible output does not prove playback.
- Audible output without `SFX_DONE` proves sound, but not acknowledged
  completion.
- Any success shown before the completion acknowledgement is a failure.
- A miss, timeout, disconnect, or ambiguous result remains part of the record.
- Do not generalize wake reliability beyond the tested distance, noise,
  orientation, and trial count.

## Gate outcome

- **Pass:** all positive routes succeed physically and truthfully in all three
  trials, and the negative control never triggers the SFX action.
- **Partial:** useful evidence was gathered, but at least one required
  observation is missing or inconsistent.
- **Fail:** the wrong action runs, success is falsely reported, playback is
  inaudible, capture is unbounded, execution is duplicated, or a route fails
  persistently.

Final classification:

Evidence summary:

Follow-up defect or retest condition:
