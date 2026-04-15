# ADR-002: AGPL v3 License Strategy

- Status: Accepted
- Date: 2025-10-20

## Context
ThreadPilot is distributed publicly and includes features that can be reused in hosted or managed environments. The project needs a copyleft license that protects openness of modifications.

## Decision
License ThreadPilot under GNU AGPL v3.

## Rationale
- Requires source disclosure of modifications, including network-served derivatives.
- Aligns with project goal of reciprocal open-source contributions.
- Provides clear legal framing for community and commercial reuse.

## Alternatives Considered
- MIT: permissive, but does not enforce contribution-back.
- GPL v3: strong copyleft, but weaker on network-use disclosure.

## Consequences
- Positive: protects community code from closed redistribution in network scenarios.
- Trade-off: some commercial adopters may avoid AGPL projects.
