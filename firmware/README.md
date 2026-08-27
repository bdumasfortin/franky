# Firmware

This directory contains Franky's ESP32 firmware, device-side assets, and physical-board evidence.

The board's USB, factory-boot, microphone, speaker, LED, wake-word, and voice-activity checks are recorded in the [hardware bring-up record](hardware-bring-up.md). That record distinguishes observed behavior from still-unverified capabilities.

## Franky device firmware

[`franky-device/`](franky-device/) currently provides the USB device side of the [Franky control board](../tools/franky-control-board/README.md). It:

- streams manual 16 kHz, 16-bit stereo recordings;
- runs the built-in **“Hi ESP”** WakeNet9 model locally;
- uses the ESP-SR Audio Front End to stop wake capture after trailing silence;
- plays connection, disconnection, and wake acknowledgement cues; and
- renders system state through the seven-pixel RGB ring.

The computer receives the bounded wake utterance and transcribes it locally. Wi-Fi/WebSocket transport remains the target deployment path and is not implemented in this firmware yet.

## Toolchain

The current build uses ESP-IDF 5.5.2. From an ESP-IDF-enabled PowerShell session:

```powershell
cd firmware/franky-device
idf.py build
.\flash.ps1 -Port COM5
```

Serial port numbers are not stable; replace `COM5` with the port shown for the connected board. Flashing overwrites the current device image, so retain the ignored factory backup described in the bring-up record.

## Sensitive data

Never commit Wi-Fi credentials or device API secrets.
