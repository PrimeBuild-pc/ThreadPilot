# Security Policy

## Supported Versions
| Version | Supported |
| --- | --- |
| 1.1.x | Yes |
| 1.0.x | Best effort |
| < 1.0 | No |

## Reporting a Vulnerability
If you discover a security issue, do not disclose it publicly before a fix is available.

1. Open a private security advisory in GitHub Security tab.
2. Include reproduction steps, impact assessment, and affected versions.
3. If possible, include logs, stack traces, and a minimal proof of concept.

## Response Targets
- Initial acknowledgment: within 72 hours.
- Triage decision: within 7 days.
- Fix timeline: based on severity and exploitability.

## Scope Notes
ThreadPilot includes privileged operations for process and power management.
Please prioritize reports involving:
- elevation and privilege boundaries,
- process manipulation safety,
- command execution and input validation,
- configuration parsing and path handling.

## Privilege Model

- The application requires administrator privileges at startup (`requireAdministrator`).
- A non-elevated startup is considered invalid and is terminated early.
- Elevated operations are still validated through security checks before execution.

## UAC Elevation Strategy

- UAC prompt is startup-scoped.
- Elevated operations are validated before execution through security checks.
- Security audit entries are emitted for allowed and denied elevated actions.

## Process Manipulation Safeguards

- Critical process denylist enforcement blocks protected targets (`System`, `csrss`, `lsass`, `wininit`).
- Security validation is enforced in process mutation paths (priority/affinity/registry-priority operations).
- Logging inputs are sanitized to reduce log-injection risk.

## Known Limitations

- Some legacy interop paths still require incremental hardening of error-code diagnostics.
- Compatibility smoke validation remains a release-candidate activity across supported Windows versions.
- Dependency updates are managed in controlled waves to reduce regression risk.

## Disclosure Process
We follow coordinated disclosure:
- confirm report,
- reproduce and triage,
- fix and validate,
- publish release notes and mitigation guidance.
