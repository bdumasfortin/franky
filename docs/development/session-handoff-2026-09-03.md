# Session handoff — September 3, 2026

Status: **Paused at the user's request; two physical-path defects block step 1.**

## Start here next session

The user rejected the practical experience with candidate v2: wake recognition
works roughly **one time in five**, and after successful recognition the
**activation beep appears in every transcript**. These are user-observed
failures, not acceptable behavior or a passed gate. The five-attempt ratio is
an estimate, not a recorded trial count. Distance, noise, exact transcript
wording, and fresh audio were not collected for this report; do not carry over
the August 27 test conditions as if they were reconfirmed.

Do not advance the approved seven-step roadmap past the physical gate on the
strength of training-corpus scores or connection/boot success. No fix was
implemented during this shutdown. Do not silently lower the threshold, retrain,
reflash, roll back, or persist ordinary wake audio as part of resuming.

## First investigation

1. Reproduce and measure the live wake failures. Compare continuous inference
   with the reset/frozen-clip diagnostic: model state and resets, input delivery,
   dropped frames, scheduling, smoothing, gating, and rearm/cooldown behavior.
   Three exact baseline board/offline clip scores validate those comparisons,
   not uninterrupted live detection or candidate v2 generalization. Determine
   whether streaming behavior, model generalization, or both explain the gap
   before choosing another training run. Keep fresh acceptance data separate
   from training data.
2. Trace wake cue playback into capture, AFE buffering, pre-roll, and local
   Whisper input. Entry points are `wake_action_task` in
   [main.c](../../firmware/franky-device/main/main.c),
   [wake_word.c](../../firmware/franky-device/main/wake_word.c), and
   [audio_board.c](../../firmware/franky-device/main/audio_board.c).
   `main.c` plays `AUDIO_CUE_WAKE_WORD`, waits `WAKE_ACKNOWLEDGEMENT_MS` (75 ms),
   then requests utterance capture. This is an inspection lead, not proof of
   root cause. Distinguish actual cue audio in the submitted PCM from a
   transcription artifact. Consider cue isolation/buffer handling without
   clipping the user's first word; do not mask the defect by removing “beep”
   text from transcripts. Use bounded, explicitly accepted private captures
   if persistence is needed.
3. Review a focused fix and retest both defects together: repeated fresh live
   wakes, representative hard negatives and idle listening, cue-only/no-speech,
   speech immediately after the cue, and disconnect/rearm. Record attempt
   counts, misses, conditions, first-word preservation, and cue contamination.
   Then repeat the existing positive commands, negative control, and SFX check.

See [physical evidence](physical-voice-path-validation.md) and the
[collection/evaluation guide](wake-word-data-collection.md). These are proposed
investigation steps, not a new architecture decision or acceptance claim.

## Exact resumption state

- Workspace: `C:\GIT\franky`.
- Branch: `codex/wake-reliability-and-spoken-loop-foundations`.
- HEAD at pause: `278dbafe6a7c8d9583dadf532ee59d3c86ab6844`; branch was four
  commits ahead of its remote, with additional uncommitted work. Preserve all
  existing changes. After the shutdown, the user explicitly requested committing
  and pushing all pending work on this branch, including this handoff.
- Candidate v2 remains flashed; “Yo Franky” microWakeWord was confirmed armed
  at 96%. No rollback or firmware change occurred during shutdown.
- Model SHA-256:
  `c984044357726ebe9ea92074614049363b2cef3fc704fac3738addb76008dc5c`.
- Firmware application SHA-256:
  `5a67954200fd285ef32547d6e2f813c8e7747a42fdb8739e0fc3221148baa837`.
- Candidate source (ignored):
  `tools/wake-word/.cache/trained/yo_franky_physical_v2/tflite_stream_state_internal_quant/stream_state_internal_quant.tflite`.
- Deployed model copy (ignored):
  `firmware/franky-device/main/models/yo_franky.tflite`.
- Original rollback model (ignored):
  `tools/wake-word/.cache/trained/yo_franky/tflite_stream_state_internal_quant/stream_state_internal_quant.tflite`;
  SHA-256 `987223a0697b9f8a382f6f00cc523026478ba99a21cef264e3686fd887b203dd`.
- The 30-positive/20-hard-negative corpus and parity recordings remain private
  under the ignored wake cache. Candidate v2's 30/30 positives and 2/20 hard-
  negative activations at 96% reuse training data, not fresh acceptance data.
- Real assistant mode: local Ollama `qwen3.5:4b`, tool selection enabled;
  Whisper `small.en`. Do not restart in demo mode.
- A Connect button bug was fixed in `app.js`: the click event must not be
  passed as a preauthorized SerialPort. Cache version is `20260903-connect1`.
- A separate Google Chrome Franky tab held COM5 and blocked the embedded tab.
  Closing that Chrome tab released COM5; reopening one embedded control-board
  tab restored USB, wake initialization, and `Ready · tools`. The earlier
  “working again” status meant connection recovery, not wake reliability.

## Restart without duplicate browser ownership

Confirm COM5 exists and no other control-board page, monitor, or flashing tool
owns it. Start the server explicitly, then open exactly one control-board tab:

```powershell
$env:FRANKY_ASSISTANT_PROVIDER = 'ollama'
$env:FRANKY_OLLAMA_MODEL = 'qwen3.5:4b'
dotnet run --project services/Franky.Runtime -- --control-board --port 8765 --web-root C:\GIT\franky\tools\franky-control-board
```

Open `http://127.0.0.1:8765/` once. The `serve.ps1` launcher also opens the
default external browser, so do not combine it with a second embedded control
tab. The passive Presence page is read-only and is not another serial owner.

## Pause validation and shutdown

- The control-board Disconnect action completed; UI reported USB disconnected.
  Its in-app tab was closed, and no control-board tabs remain in this task.
- The session's Franky runtime, `dotnet run`, and launcher processes exited.
  Port 8765 has no listener. No training, flashing, or handle-diagnostic process
  remains in the checked process list. COM5 opens successfully for a status
  check and is not left locked by the test.
- The session-loaded `qwen3.5:4b` model was unloaded; Ollama reports zero loaded
  models. The pre-existing Ollama tray/server (started before this session),
  Codex, editor services, and unrelated Blueprint work were left running.
- Candidate v2 and the private dataset/model artifacts were preserved. No new
  audio or transcript was saved for this pause. The later commit/push request
  does not include these ignored private artifacts.
- Documentation link/image checks: 98 local references across 37 Markdown files,
  no broken targets. `git diff --check` and the control-board JavaScript syntax
  check passed. Firmware builds and runtime tests were not rerun for this
  documentation-only pause. Earlier automated results do not negate the later
  physical failures.

For the subsequent commit/push checkpoint, the Release build, all 24 runtime
tests, formatting verification, and JavaScript/Python/PowerShell/YAML syntax
checks passed. Firmware was not rebuilt or reflashed during checkpointing; the
earlier built/flashed image and its failed physical trial remain the evidence.
