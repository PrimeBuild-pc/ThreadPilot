# Release signing guide

ThreadPilot's public releases are currently unsigned. The SignPath integration has passed a `workflow_dispatch` run with the self-signed test certificate. The production certificate is pending SignPath Foundation review.

## Validate test signing

1. Dispatch the GitHub **Release** workflow with `dry_run_publish=true`.
2. Check the two SignPath requests for the application binaries and Inno Setup installer. The workflow packages the signed executable into the installer and portable ZIP, then calculates the published SHA-256 checksums.
3. Check Authenticode verification and the smoke test in the run summary. The `signpath-signed-zips` Actions artifact contains the two ZIPs returned by SignPath.

The workflow uses the `SIGNPATH_API_TOKEN` repository secret for the SignPath CI user. Test mode skips GitHub and WinGet publication. Chocolatey uses its own workflow.

## Enable production signing

1. After SignPath Foundation activates the release certificate, set the GitHub Actions repository variable `SIGNPATH_SIGNING_POLICY_SLUG` to `release-signing`. The production workflow requires this configuration and verifies the signatures before publication.
2. Use the repository's verified merge and release process to prepare the release tag.
3. Review and approve the application and installer requests in SignPath as they appear. The installer uses the signed application from the first request.
4. Approve publishing through the protected GitHub `release` environment after verifying the release tag and artifacts.

## Verify a published artifact

```powershell
Get-AuthenticodeSignature .\ThreadPilot_v1.2.3_Setup.exe
Get-FileHash .\ThreadPilot_v1.2.3_Setup.exe -Algorithm SHA256
```

Compare the hash with `SHA256SUMS.txt` from the same release. See the [Code signing policy](CODE_SIGNING_POLICY.md) for the signing scope and team roles.
