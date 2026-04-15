# ADR-003: Process Event Monitoring via WMI + Polling Fallback

- Status: Accepted
- Date: 2025-10-20

## Context
ThreadPilot must react to process start/stop reliably on end-user systems with varying permissions and OS configurations.

## Decision
Use WMI event watchers as primary signal source with resilient polling fallback.

## Rationale
- WMI provides straightforward process lifecycle events with broad compatibility.
- Polling fallback covers degraded WMI scenarios and service outages.
- Existing architecture already integrates process snapshots and incremental updates.

## Alternatives Considered
- ETW-only approach: high performance and fidelity, but higher implementation complexity and operational variability.
- Polling-only approach: simpler, but less responsive and less efficient.

## Consequences
- Positive: balanced reliability and compatibility.
- Trade-off: must handle WMI hangs/timeouts and race conditions carefully (mitigated with timeout scopes, disposal guards, and fallback recovery).
