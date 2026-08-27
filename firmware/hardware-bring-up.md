# Hardware Bring-up Record

Status: **USB development path verified; network and auxiliary hardware remain open**

Observed on: **2026-08-26**

## Physical sample

- Product marking: `ESP32-S3-AUDIO-Board`.
- No board revision is printed or otherwise specified on the received sample.
- Package contents used for this check: board only, with a user-supplied USB-C-to-USB-A data cable.

The exact Waveshare board revision therefore remains unresolved. The ESP32-S3 silicon revision was read electronically and is recorded separately below.

## Observed evidence

- Windows enumerated an Espressif USB Serial/JTAG interface and a serial port (`COM5` in this session; the port number is not assumed stable).
- The device identified as an ESP32-S3 QFN56, silicon revision v0.2, with an integrated USB Serial/JTAG interface and a 40 MHz crystal.
- The device reported 8 MB of embedded PSRAM.
- The external SPI flash reported a capacity of 16 MB and 3.3 V operation.
- Secure Boot and Flash Encryption both reported disabled on this sample.
- A 115200-baud startup log identified the factory firmware project as `xiaozhi` and continued to emit stable free-memory telemetry.
- The factory firmware visibly flashed the RGB ring blue and produced Chinese speech through the speaker during startup and diagnostic activity.
- A complete 16 MB read-only backup of the factory flash was captured before any write operation.
- The custom Franky firmware built, flashed, booted, and returned 16 kHz, 16-bit stereo samples from both onboard microphones over USB.
- A disposable one-second capture contained 16,000 frames per microphone with nonzero samples and closely matched signal levels.
- The user judged later recordings to be very good from close range through roughly 12 feet using 30 dB input gain and raw stereo.
- The seven-pixel RGB ring was manually verified across the project state colors, including the amber offline breathing animation.
- The WakeNet9 **“Hi ESP”** model was built, flashed, initialized, and repeatedly triggered from spoken input.
- A locally trained 62,304-byte microWakeWord **“Yo Franky”** model was built
  into firmware, flashed, booted, and reported as the active engine. After a
  cooperative feed-task scheduling fix, the board remained idle for at least
  15 seconds without a watchdog event.
- The user then spoke **“Yo Franky”** to the physical board and reported that
  detection worked very nicely through the existing cue, capture, and control-board flow.
- After wake detection, the ESP-SR Audio Front End captured speech until trailing silence and delivered bounded mono audio for local Whisper transcription.
- Connection, disconnection, and wake acknowledgement cues were heard through the ES8311 speaker path.

These observations verify the USB data path, MCU identification, memory capacities, factory-firmware boot, stereo microphone capture, local wake detection, voice-activity endpointing, state LEDs, speaker cues, and the complete wake-to-transcript development path. They do not verify the planned Wi-Fi transport or full spoken-response playback.

## Factory backup

- Local path: `firmware/backups/esp32-s3-audio-board_factory_2026-08-26.bin`
- Size: 16,777,216 bytes
- SHA-256: `C6ED875EA85F94EA7166ADE047686D149AB8379510C35CF1EAE837D9178AD2DB`
- Repository handling: `firmware/backups/` is ignored by Git.

Treat the image as device-specific local recovery material. Do not publish or commit it. The backup was captured before the first write; the board has since been flashed with the custom Franky firmware.

## Development toolchain prepared

- Espressif Installation Manager CLI 0.18.0, installed through WinGet.
- ESP-IDF 5.5.2.
- Espressif `esptool.py` 4.12.0.

ESP-IDF 5.5.2 was prepared in response to the bring-up review choice. It is an evaluation toolchain, not an accepted permanent firmware-framework decision.

## Still unverified or incomplete

- Buttons and other user inputs.
- Full response-audio playback beyond short system cues.
- microSD/TF-card operation.
- Battery or external-power behavior.
- Wi-Fi connectivity.
- Restoration of the factory backup after custom firmware use.
- Longer-term “Yo Franky” miss rate and false-activation behavior in ordinary use.
