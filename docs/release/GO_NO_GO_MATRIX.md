# Go/No-Go Decision Matrix

## Decision Inputs

| Area | Status | Blocking? | Notes |
|---|---|---|---|
| Build | Complete | Yes | Release build currently passing |
| Tests | Complete | Yes | Core test suite currently green |
| Security scan | Complete | Yes | No vulnerable packages and no secret leaks |
| Packaging | Complete | Yes | Installer, portable zip artifacts and checksums generated |
| Chocolatey validation artifacts | Complete | Yes | Tagged or dry-run releases must preserve `.nupkg` and metadata evidence |
| winget publication evidence | Complete | Yes | Tagged releases must either submit winget or fail with explicit missing-secret policy |
| Chocolatey publication evidence | Complete | Yes | Tagged releases must either attempt Chocolatey publish or fail with explicit missing-secret policy |
| Documentation | Complete | Yes | Release docs and governance artifacts updated |
| Compatibility smoke tests | Deferred | Yes | Execute on final release candidate |

## Decision Rule

- GO: all blocking rows are complete.
- GO for manual dry-run only if publication evidence rows explicitly record `dry-run` and the release is not being treated as public.
- GO for manual dry-run only if Chocolatey validation artifacts are also present (`.nupkg` + metadata JSON).
- NO-GO: any blocking row incomplete, unless explicit waiver approved.

## Waiver Log

| Date | Area | Approved By | Rationale | Expiry |
|---|---|---|---|---|
