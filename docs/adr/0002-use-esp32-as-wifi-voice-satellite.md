# ADR-0002: Use the ESP32 as a Wi-Fi voice satellite

- Status: Accepted
- Date: 2026-08-18
- Deciders: Project owner

## Context

The selected Waveshare ESP32-S3-AUDIO-Board provides microphones, a speaker, buttons, LEDs, Wi-Fi, and USB. The computer is intended to run the heavier assistant software. The project owner confirmed that audio and commands may travel over Wi-Fi.

## Decision

Use the ESP32 as a networked voice satellite:

- Wi-Fi carries assistant audio, events, and responses between the board and computer-hosted system.
- USB provides power, initial firmware flashing, and development diagnostics.
- The ESP32 must run device firmware responsible for its audio hardware and network integration.
- Heavy speech, intent, automation, and response processing belongs on the computer unless a later ADR assigns a narrow function to the device.

## Consequences

### Positive

- Provides a clean hardware/software boundary for custom development.
- Keeps resource-intensive processing off the microcontroller.
- Allows the board to be placed independently of the computer after development.

### Negative

- The assistant depends on 2.4 GHz Wi-Fi quality and computer availability.
- Network authentication and failure handling become part of the product.
- USB connection alone will not expose the board as a plug-and-play computer microphone and speaker.

### Follow-up

- Measure audio and connection behavior during physical-board bring-up.
- Define authentication, reconnect behavior, and unavailable-service feedback.
- Select the embedded framework and specify the project-owned wire protocol.
