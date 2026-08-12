# Release Signing Guide

ThreadPilot releases are currently unsigned. An application for SignPath Foundation code signing is pending; the release workflow remains fully automated and unchanged until the project is approved.

## Intended signing model

- Sign the published executable and installer with Authenticode through SignPath.
- Use the publisher identity assigned by SignPath Foundation.
- Submit signing requests only from the protected GitHub release workflow.
- Keep SignPath project identifiers and organization secrets in GitHub environment secrets.
- Require an explicit SignPath approval when the selected policy requires it.

Signing will be inserted between artifact creation and checksum generation. Builds, tests, packaging, GitHub Release creation, and artifact upload remain automated; no private certificate or PFX will be stored in the repository or on a GitHub runner.

## Status and rollout

1. Complete the SignPath Foundation application.
2. Wait for approval and the SignPath organization/project invitation.
3. Configure the SignPath project and trusted build policy.
4. Add the identifiers and token supplied by SignPath as protected GitHub secrets.
5. Integrate signing on a separate change and test it with a prerelease tag.
6. Verify the Authenticode publisher, timestamp, checksums, and installer before publishing a stable release.

Until those steps are complete, release artifacts must be described as unsigned. Do not enable the legacy PFX signing path without a separately acquired certificate and a reviewed secret-storage procedure.

## Verification

```powershell
Get-AuthenticodeSignature .\ThreadPilot.exe
Get-FileHash .\ThreadPilot.exe -Algorithm SHA256
```

See the public [code-signing policy](CODE_SIGNING_POLICY.md) for scope, approvals, and incident handling.
