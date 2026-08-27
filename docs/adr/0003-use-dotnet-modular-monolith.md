# ADR-0003: Use a .NET modular monolith for the computer runtime

- Status: Accepted
- Date: 2026-08-18
- Deciders: Project owner, engineering recommendation

## Context

The project owner delegated the language choice while prioritizing runtime speed and avoiding unnecessary limitations. The initial host is a Windows computer with .NET 10, Python 3.14, Node.js 24, and Rust 1.93 installed.

The v1 workload is primarily asynchronous network I/O, model latency, bounded process execution, and later audio streaming. It does not require a distributed service architecture.

## Considered options

### Python

Offers the broadest direct ecosystem for local speech and machine-learning experiments, but provides weaker runtime performance and deployment discipline for a continuously running coordinator.

### C# and .NET 10

Provides strong asynchronous I/O, process control, WebSocket support, structured application architecture, Windows integration, cross-platform deployment, and good test tooling. Local models can remain behind adapters or external process boundaries.

### Rust

Provides excellent performance and control, but adds integration cost for model, media, and API ecosystems without improving the dominant v1 latency sources.

## Decision

Build the computer runtime as a **.NET 10 modular monolith**. Keep conversation providers, capabilities, command execution, device transport, and media integrations behind explicit interfaces. Split a module into another process only after measurement or dependency isolation demonstrates a need.

## Consequences

### Positive

- Strong performance for a long-running Windows host.
- No runtime framework packages are required for the initial console application.
- Future WebSocket and media integrations fit the platform.
- Local Python or native model runtimes can be integrated without making them the orchestration language.

### Negative

- Some local AI libraries may require process or native-library adapters.
- The project must maintain clear module boundaries inside one deployment.

### Follow-up

- Benchmark actual audio and model paths before optimizing or splitting services.
- Keep the OpenAI and future local-model providers replaceable.

