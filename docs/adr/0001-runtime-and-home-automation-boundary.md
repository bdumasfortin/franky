# ADR-0001: Build a standalone custom assistant

- Status: Accepted
- Date: 2026-08-18
- Deciders: Project owner

## Context

The ESP32 will act as a Wi-Fi voice endpoint while heavier processing runs on a computer. The term “home assistant” initially caused ambiguity with the official Home Assistant platform. The project owner clarified that the goal is to code a standalone DIY assistant, including the software that runs on both the PC and ESP32.

This decision establishes the product boundary. It does not require implementing speech recognition, text-to-speech, or machine-learning algorithms from first principles; that dependency boundary requires a separate decision.

## Decision drivers

- Reach a useful end-to-end voice interaction with limited unnecessary infrastructure.
- Preserve room for custom assistant behavior later.
- Own the assistant architecture and behavior rather than adopting an existing assistant platform.
- Keep the intent/action boundary testable independently of board readiness.
- Make action outcomes safe and observable.

## Considered options

### Option A: Extend the official Home Assistant platform

Use official Home Assistant and its established voice pipeline, adding only project-specific integrations.

- Lower custom maintenance burden.
- Does not meet the project goal of building the assistant architecture.

### Option B: Add a custom companion to Home Assistant

Use Home Assistant as the control plane while custom software owns selected orchestration or conversation behavior.

- Preserves platform interoperability.
- Still makes an existing assistant platform a foundational dependency.

### Option C: Standalone custom assistant

Build the device firmware, transport, request handling, integrations, and product behavior as project-owned software.

- Matches the explicit project goal.
- Provides maximum control and learning opportunity.
- Carries the largest security, reliability, testing, and maintenance burden.

## Decision

Choose **Option C: Standalone custom assistant**. Do not depend on the official Home Assistant platform. Write and maintain the PC application, ESP32 firmware, network protocol, action boundary, and user-facing behavior in this repository.

## Consequences if accepted

### Positive

- The architecture directly serves the project’s learning and ownership goals.
- Device and computer behavior can evolve together under one explicit protocol.
- There is no foundational dependency on an existing assistant platform.

### Negative

- The project must implement and secure more infrastructure itself.
- Device discovery, action verification, observability, upgrades, and recovery cannot be delegated to a platform.
- Careless interpretation of “from scratch” could expand v1 into speech-model research rather than a usable assistant.

### Follow-up

- Decide which general-purpose libraries and pretrained models may be reused.
- Select the PC language, ESP32 framework, activation mode, privacy boundary, and v1 capability scope.
- Define the first text-based request seam and one real integration.
