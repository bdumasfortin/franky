![Franky — DIY Voice Assistant](docs/assets/franky-readme-banner.png)

# Franky

Franky is a personal, from-scratch voice assistant built around a
[Waveshare ESP32-S3-AUDIO-Board](https://www.waveshare.com/esp32-s3-audio-board.htm)
and a custom .NET runtime on the computer. The board handles room audio, the
wake word, speaker cues, and status LEDs; the computer handles local speech
recognition, conversation, and safe command execution.

> **Current state:** the USB development path works end to end from a wake
> phrase to a local transcript. A custom **“Yo Franky”** model is now trained,
> flashed, and able to trigger, but a broader physical check found unacceptable
> sensitivity: it sometimes required roughly ten repetitions at 20 inches in a
> quiet room. Wake tuning is now the active physical blocker.
> A diagnostic firmware build with temporary sensitivity control and
> metadata-only near-miss scores is flashed. A 96% versus 87% comparison yielded
> roughly three detections per ten attempts at both settings, so representative
> physical samples and model tuning are needed rather than a lower cutoff alone.
> A private guided Dataset workflow is now built and flashed: it captures the
> exact processed signal used by the wake model, requires review and explicit
> acceptance before local storage, and includes an offline evaluator. The first
> 30-positive/20-hard-negative spoken corpus is complete. Offline scoring at the
> current 96% cutoff detected 25/30 positives but also activated on 5/20 hard
> negatives; no tested cutoff from 50–99% separated the two classes. Board versus
> evaluator score parity is the next evidence gate before a retrained candidate.
> The control board now hands completed transcripts to Franky's conversation
> and allowlisted-command path. Ollama with `qwen3.5:4b` is the selected local
> provider, while OpenAI remains an optional cloud adapter. The HTTP bridge and
> both provider tool loops are locally tested, and live Ollama selected both
> read-only diagnostics correctly through the loopback endpoint. After
> successful physical wakes, both diagnostics answered correctly and the
> longer negative control behaved as expected. The first named device
> action is implemented and flashed: asking Franky how it is going requests an
> embedded “SUUUPER” clip and waits for the board to acknowledge completion.
> Direct serial start/completion, live Ollama intent selection, and audible
> physical playback are verified with truthful status. Wi-Fi
> transport and generated spoken answers remain ahead. A provider-neutral,
> cancellable local TTS boundary is implemented and tested, but no engine or
> Franky voice has been selected and no generated audio reaches the board yet.
> The passive **Franky
> Presence** page now receives the truthful USB-session lifecycle from the open
> control-board tab; a runtime-owned feed remains ahead.

## What works today

- Far-field stereo microphone capture from close range to roughly 12 feet.
- On-device wake detection: the previously verified **“Hi ESP”** WakeNet path
  remains a fallback, while the custom **“Yo Franky”** microWakeWord path is
  installed and able to trigger on the physical board but currently has an
  unacceptable miss rate.
- Natural utterance capture that stops after trailing silence.
- Local speech-to-text with Whisper `small.en`, accelerated by an NVIDIA GPU
  when CUDA is available and backed by a CPU fallback.
- Speaker cues for connection, disconnection, and wake acknowledgement, plus
  an embedded named “SUUUPER” clip.
- A seven-pixel status ring with state colors and an offline breathing animation.
- A local browser control board for audio, LED, wake-word, and device testing.
- A deliberate local-only wake-dataset collector with review-before-save,
  deletion controls, ignored storage, and current-model offline evaluation.
- A .NET conversation pipeline with local Ollama, deterministic demo, optional
  OpenAI providers, and strictly allowlisted read-only commands.
- A loopback assistant-turn endpoint that connects wake transcripts to that
  conversation path and reports model-selected actions separately from replies.
- A fixed `device.sfx.frankys_suuuper` action that maps natural questions about
  how Franky is doing to one firmware command; the UI reports success only
  after the ESP32 finishes playback.
- A local exact-intent router for common forms such as “How's it going?” so this
  signature response does not depend on probabilistic model tool selection.
- A provider-neutral speech-synthesis contract with bounded board-compatible
  PCM output, single-flight execution, cooperative cancellation, and
  metadata-only diagnostics; production synthesis and playback remain pending.
- A passive Franky Presence display for the latest transcript, reply, and
  truthful lifecycle activity, with privacy, offline, error, narrow-screen, and
  reduced-motion presentations. The current live source is the control-board
  browser tab; deterministic mock states remain available in its test harness.

## How it fits together

```text
speech
  ↓
ESP32-S3 board ── USB serial today / Wi-Fi later ──> Franky runtime
  │                                                    │
  ├─ “Yo Franky” microWakeWord                         ├─ local Whisper speech-to-text
  │  (WakeNet fallback)                                │
  ├─ voice activity detection                         ├─ conversation and safe commands
  ├─ microphones + speaker                            └─ control-board web app
  └─ animated status LEDs
```

The browser control board is intentionally a development tool. The intended
room setup uses Wi-Fi between the ESP32 and the computer, with USB retained for
power, flashing, and diagnostics.

Franky Presence is a separate passive display rather than a second control
surface. In the current USB architecture, the control-board tab publishes its
authoritative device and turn state over an ephemeral same-origin browser
channel. The eventual runtime-owned, one-way display feed remains planned.

## Try the control board

With the Franky firmware already flashed and the board connected over USB:

```powershell
.\tools\franky-control-board\serve.ps1
```

Open the loopback page, choose **Connect to Franky**, and select the Espressif
USB serial device. The Wake area shows the phrase actually armed by the board.
On the current build, say **“Yo Franky”**, wait for the acknowledgement cue,
then speak naturally. The recognized text appears in the Wake area and terminal.
Ask **“How is it going?”** to request Franky's embedded “SUUUPER” response.
The launcher uses local Ollama with `qwen3.5:4b` by default, so that transcript
continues through Franky's conversation provider and may invoke one of the two
fixed read-only diagnostics without a cloud key. Use `-AssistantProvider demo`
for the deterministic **Demo · no tools** mode or `-AssistantProvider openai`
after configuring a separate OpenAI Platform key.

To work on wake reliability, open **Dataset**. The first set of 30 positive “Yo
Franky” samples and 20 hard negatives has been collected and evaluated. Each
recording stays in memory until **Keep sample** is pressed, and accepted files
remain only below the ignored local wake-word cache. See the
[collection guide](docs/development/wake-word-data-collection.md).

The first run downloads the Whisper `small.en` model to
`%LOCALAPPDATA%\Franky\models`. Wake audio remains local and is discarded after
transcription. In the default Ollama mode, transcript text and conversation
history stay in memory on this computer and are not persisted by Franky. When a
cloud provider is selected, transcript text crosses that provider boundary.

See the [control-board guide](tools/franky-control-board/README.md) for the full
testing flow and [firmware guide](firmware/README.md) for board setup.

## Try the passive presence page

Start the control board normally, then choose **Presence ↗** or open
`http://127.0.0.1:8765/presence/`. Keep the control-board tab open so it can
publish the current USB lifecycle. The presence page itself contains no
commands or settings; `harness.html` provides separate deterministic controls.

See the [Franky Presence guide](tools/franky-presence/README.md) for the exact
launch command, fixed-state queries, current limitations, and privacy boundary.

## Develop the runtime

Build and run the automated checks:

```powershell
dotnet build Franky.slnx --configuration Release
dotnet run --project tests/Franky.Runtime.Tests --configuration Release
```

Run a local text conversation without API credentials:

```powershell
$env:FRANKY_ASSISTANT_PROVIDER = "ollama"
dotnet run --project services/Franky.Runtime
```

Install and model setup are in the [local Ollama guide](docs/development/local-ollama.md).
The optional OpenAI conversation provider requires a separately created
`OPENAI_API_KEY` and API-platform billing or credits. A ChatGPT subscription is
not an application credential. Setup and privacy details live in the
[OpenAI development notes](docs/development/openai-api.md).

## Repository guide

| Path | Purpose |
| --- | --- |
| [`firmware/franky-device/`](firmware/franky-device/) | ESP32 firmware for microphones, wake detection, embedded audio, speaker cues, and LEDs |
| [`services/Franky.Runtime/`](services/Franky.Runtime/) | Computer-hosted .NET runtime and local transcription service |
| [`tools/franky-control-board/`](tools/franky-control-board/) | Local browser interface for developing and testing Franky |
| [`tools/franky-presence/`](tools/franky-presence/) | Passive always-open display, served by the control-board app with a separate deterministic harness |
| [`tools/wake-word/`](tools/wake-word/) | Reproducible local training workspace for “Yo Franky” |
| [`tests/Franky.Runtime.Tests/`](tests/Franky.Runtime.Tests/) | Runtime and safety-boundary checks |
| [`docs/`](docs/) | Product, architecture, development, and decision documentation |

Start with the [documentation index](docs/README.md) for the project’s current
direction and the distinction between working features and planned ones. The
[spoken-loop roadmap](docs/plan/spoken-loop-roadmap.md) records the approved
seven-step implementation sequence and its decision gates.

## Privacy and safety

- Do not commit API keys, Wi-Fi credentials, access tokens, recordings, or transcripts.
- Local wake-word transcription does not send speech outside the computer.
- Local Ollama conversation keeps transcript text and replies on the computer.
- OpenAI-backed conversation is a separate, explicit cloud boundary.
- Model-requested actions are mapped to fixed, allowlisted commands; arbitrary
  model-generated shell commands are rejected.

## License

Franky is available under the [MIT License](LICENSE).
