# Franky Control Board

A local control board for Franky, the DIY voice assistant running on the Waveshare ESP32-S3-AUDIO-Board. Its state-driven interface includes an animated system core, a persistent event terminal, and separate Audio, LED, Wake, and Device areas. The offline screen stays focused on connecting to Franky.

The Audio area captures both onboard microphones as 16 kHz, 16-bit PCM over USB and creates playable WAV clips in the browser. It can listen to either microphone, a mono mix, or raw stereo, with selectable hardware input gain.

The firmware continuously recognizes **“Hi ESP”** on the ESP32-S3 with WakeNet. The speaker plays a short rising cue when the browser connects, the matching descending cue when it disconnects, and a brighter acknowledgement when the wake word is heard. After a detection, ESP-SR voice activity detection listens until speech ends, sends a bounded mono utterance to the computer, and local Whisper transcription appears in the Wake area and terminal. Manual raw-stereo recording remains available between wake detections.

## Run

1. Flash `firmware/franky-device` to the board.
2. Run `./serve.ps1` from this directory. This starts the .NET control-board service on loopback. The first run downloads the `small.en` Whisper model (roughly 466 MiB) to `%LOCALAPPDATA%\Franky\models`.
3. In the Chromium-based page that opens, choose **Connect to Franky** and select the Espressif USB serial device.
4. Say **“Hi ESP”**, wait for the acknowledgement, and speak naturally. Franky stops after trailing silence and shows the local transcript. Use the Audio area for manual recordings.

Manual clips remain in browser memory unless downloaded. Wake clips are discarded after transcription, transcript text is not persisted, and the page does not send speech outside the computer. The one-time model download is the only external request in the local speech path.
