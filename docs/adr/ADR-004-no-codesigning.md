# ADR-004: Release Without Code Signing Certificate

- Status: Accepted
- Date: 2025-10-20

## Context
ThreadPilot currently has no purchased code-signing certificate. Releases must continue while reducing trust friction for users.

## Decision
Ship unsigned binaries and document trust mitigations.

## Mitigations
- Publish SHA256 checksums for all release artifacts.
- Provide transparent source code and reproducible build scripts.
- Maintain SBOM and security scanning in CI workflows.
- Document SmartScreen/UAC expectations in troubleshooting docs.

## Alternatives Considered
- Delay all releases until certificate procurement.
- Restrict distribution channels only to package managers.

## Consequences
- Positive: release velocity is maintained.
- Trade-off: SmartScreen warnings are expected until reputation or signing changes.
