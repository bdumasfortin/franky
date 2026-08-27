# ADR-0006: Expose only allowlisted named commands to the model

- Status: Accepted
- Date: 2026-08-18
- Deciders: Engineering safety boundary

## Context

V1 should prove that a conversation can cause software to execute a script or command. Passing model-generated text directly to a shell would allow prompt injection or model error to become arbitrary code execution.

## Decision

- The model may call a structured `run_named_command` function.
- Its argument is an enum-like command identifier supplied by the application.
- Each identifier maps to a fixed executable and fixed arguments in trusted code or configuration.
- V1 exposes only read-only commands.
- Model-supplied executable paths, arguments, shell syntax, and environment variables are rejected.
- Future state-changing commands require a separate confirmation and authorization design.

## Consequences

### Positive

- Demonstrates real tool execution without arbitrary shell access.
- Commands can be logged, tested, and authorized individually.
- Future Plex and casting capabilities can use purpose-built adapters instead of shell strings.

### Negative

- Every supported command requires deliberate implementation.
- This is less flexible than a general shell tool by design.

