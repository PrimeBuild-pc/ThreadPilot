# Release Signing Guide

This document describes how to prepare ThreadPilot for signed production releases.

## Objectives
- Authenticate official binaries.
- Reduce SmartScreen friction.
- Provide traceability for shipped artifacts.

## Recommended Strategy
1. Use an Authenticode code-signing certificate from a trusted CA.
2. Store signing secrets in GitHub Environments with required approvals.
3. Sign binaries only in protected release workflows triggered by tags.

## Local Signing Example
```powershell
signtool sign /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 /a "ThreadPilot.exe"
```

## CI Signing Steps
1. Decode PFX from encrypted secret at workflow runtime.
2. Import certificate into current user certificate store.
3. Sign release binaries and installer artifacts.
4. Publish SHA256 checksums and signature verification instructions.

## Verification
```powershell
Get-AuthenticodeSignature .\ThreadPilot.exe
Get-FileHash .\ThreadPilot.exe -Algorithm SHA256
```

## Operational Notes
- Never commit certificates or private keys.
- Rotate secrets periodically.
- Require maintainer approval for release jobs.
