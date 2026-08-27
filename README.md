![Franky — DIY Voice Assistant](docs/assets/franky-readme-banner.png)

# Franky

Franky is a personal, from-scratch voice assistant built around a
[Waveshare ESP32-S3-AUDIO-Board](https://www.waveshare.com/esp32-s3-audio-board.htm)
and a custom .NET runtime on the computer. The board handles room audio, the
wake word, speaker cues, and status LEDs; the computer handles local speech
recognition and will grow into conversation and home-control features.

> **Current state:** the USB development path works end to end from a wake
> phrase to a local transcript. A custom **“Yo Franky”** model is now trained,
> flashed, and successfully recognized in its first physical-room test. Longer
> use will reveal whether its sensitivity needs tuning.
> Wi-Fi transport, command interpretation, and spoken answers remain ahead.

## What works today

- Far-field stereo microphone capture from close range to roughly 12 feet.
- On-device wake detection: the previously verified **“Hi ESP”** WakeNet path
  remains a fallback, while the custom **“Yo Franky”** microWakeWord path is
  installed and verified by a successful spoken test on the physical board.
- Natural utterance capture that stops after trailing silence.
- Local speech-to-text with Whisper `small.en`, accelerated by an NVIDIA GPU
  when CUDA is available and backed by a CPU fallback.
- Speaker cues for connection, disconnection, and wake acknowledgement.
- A seven-pixel status ring with state colors and an offline breathing animation.
- A local browser control board for audio, LED, wake-word, and device testing.
- A .NET conversation pipeline with a deterministic demo provider, an optional
  OpenAI provider, and strictly allowlisted read-only commands.

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

## Try the control board

With the Franky firmware already flashed and the board connected over USB:

```powershell
.\tools\franky-control-board\serve.ps1
```

Open the loopback page, choose **Connect to Franky**, and select the Espressif
USB serial device. The Wake area shows the phrase actually armed by the board.
On the current build, say **“Yo Franky”**, wait for the acknowledgement cue,
then speak naturally. The recognized text appears in the Wake area and terminal.

The first run downloads the Whisper `small.en` model to
`%LOCALAPPDATA%\Franky\models`. Wake audio and transcripts remain in memory,
stay on the computer, and are not persisted.

See the [control-board guide](tools/franky-control-board/README.md) for the full
testing flow and [firmware guide](firmware/README.md) for board setup.

## Develop the runtime

Build and run the automated checks:

```powershell
dotnet build Franky.slnx --configuration Release
dotnet run --project tests/Franky.Runtime.Tests --configuration Release
```

Run a local text conversation without API credentials:

```powershell
dotnet run --project services/Franky.Runtime -- --demo
```

The optional OpenAI conversation provider requires a separately created
`OPENAI_API_KEY` and API-platform billing or credits. A ChatGPT subscription is
not an application credential. Setup and privacy details live in the
[OpenAI development notes](docs/development/openai-api.md).

## Repository guide

| Path | Purpose |
| --- | --- |
| [`firmware/franky-device/`](firmware/franky-device/) | ESP32 firmware for microphones, wake detection, speaker cues, and LEDs |
| [`services/Franky.Runtime/`](services/Franky.Runtime/) | Computer-hosted .NET runtime and local transcription service |
| [`tools/franky-control-board/`](tools/franky-control-board/) | Local browser interface for developing and testing Franky |
| [`tools/wake-word/`](tools/wake-word/) | Reproducible local training workspace for “Yo Franky” |
| [`tests/Franky.Runtime.Tests/`](tests/Franky.Runtime.Tests/) | Runtime and safety-boundary checks |
| [`docs/`](docs/) | Product, architecture, development, and decision documentation |

Start with the [documentation index](docs/README.md) for the project’s current
direction and the distinction between working features and planned ones.

## Privacy and safety

- Do not commit API keys, Wi-Fi credentials, access tokens, recordings, or transcripts.
- Local wake-word transcription does not send speech outside the computer.
- OpenAI-backed conversation is a separate, explicit cloud boundary.
- Model-requested actions are mapped to fixed, allowlisted commands; arbitrary
  model-generated shell commands are rejected.

## License

Franky is available under the [MIT License](LICENSE).
