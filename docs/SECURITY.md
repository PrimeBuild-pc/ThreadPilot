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

## Disclosure Process
We follow coordinated disclosure:
- confirm report,
- reproduce and triage,
- fix and validate,
- publish release notes and mitigation guidance.
