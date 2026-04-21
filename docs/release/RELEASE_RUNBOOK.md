# Release Execution Runbook

Date: 2026-04-15

## Pre-Release (T-48h)

1. Confirm version in project metadata and release notes draft.
2. Run full build/test/security commands.
3. Confirm quality gates in docs/QUALITY_GATES.md.
4. Prepare release notes from docs/release/RELEASE_NOTES_TEMPLATE.md.

## Release Build (T-0)

1. Build installer:

```powershell
./build/build-installer.ps1 -Version "<version>"
```

2. Build ZIP packages:

```powershell
./build/package-release-zips.ps1 -Version "<version>"
```

3. Generate checksums:

```powershell
$hashFile = "artifacts/release/SHA256SUMS.txt"
if (Test-Path $hashFile) { Remove-Item $hashFile -Force }
Get-ChildItem "artifacts/release" -Recurse -File -Include *.zip,*.exe |
ForEach-Object {
  $hash = Get-FileHash $_.FullName -Algorithm SHA256
  "$($hash.Hash)  $($_.Name)" | Out-File -FilePath $hashFile -Append -Encoding utf8
}
```

## Publish

Source-of-truth policy:

- GitHub release tag and assets are the canonical release source.
- Winget and Chocolatey metadata must reference an existing GitHub release tag and reachable asset URLs.
- Changelog generation in CI must run with runner-native `git-cliff` binary (pinned version + checksum verification), not Docker-based changelog actions.
- Tagged releases are public-release mode: missing winget or Chocolatey publication secrets are a release blocker, not a silent skip.
- `workflow_dispatch` may be run with `dry_run_publish=true` to validate build, assets, summaries, and packaging flow without external publication.
- Every publish channel must leave evidence behind via `GITHUB_STEP_SUMMARY` plus uploaded log artifacts.

Use script automation (requires authenticated gh CLI):

```powershell
./build/create-github-release.ps1 -Version "<version>" -NotesFile "docs/release/RELEASE_NOTES.md"
```

Manual fallback:

1. Create tag: `git tag -a v<version> -m "Release v<version>"`
2. Push tag: `git push origin v<version>`
3. Create GitHub release and upload artifacts.

Package publication prechecks:

1. Confirm `v<version>` exists in GitHub releases.
2. Confirm installer URL resolves for the exact version.
3. Confirm SHA256 in package metadata matches the published installer.
4. Confirm winget and Chocolatey package versions match the GitHub release version.
5. Confirm release workflow changelog step executed successfully with the pinned `git-cliff` version.
6. Confirm release mode is intentional:
   - tagged release: publish secrets present and external publication expected
   - manual dry run: `dry_run_publish=true` and no external publication expected
7. Confirm the generated winget manifest artifact contains exactly:
   - `PrimeBuild.ThreadPilot.yaml`
   - `PrimeBuild.ThreadPilot.locale.en-US.yaml`
   - `PrimeBuild.ThreadPilot.installer.yaml`
8. Confirm README/download docs still point users to the latest release page or current asset naming contract.

Publication evidence checklist:

1. Review the release workflow summary for a channel result line:
   - `submitted` for winget
   - `published` for Chocolatey
   - `dry-run` only for manual dry-run executions
2. Download and inspect the uploaded channel logs:
   - `winget-submit-log.txt`
   - `choco-pack-log.txt`
   - `choco-push-log.txt`
3. Download and inspect Chocolatey packaging artifacts:
   - generated `.nupkg`
   - `chocolatey-package-metadata.json`
   - `chocolatey-publish-metadata.json` when publish was attempted
4. Treat a tagged release with missing channel evidence as a failed release, even if GitHub release assets were created.

## Post-Release (T+24h)

1. Verify downloadable artifacts are accessible.
2. Execute smoke installation on clean environment.
3. Monitor issue tracker for regressions.
4. Document hotfix decisions if needed.
5. Verify package channel discoverability:
   Run `winget source update`, then `winget search --id PrimeBuild.ThreadPilot -s winget`.
   Run `choco search threadpilot --exact` and confirm moderation state on the package page.
   If Chocolatey search is still negative after publish, inspect moderation state before treating it as a release regression.
   Archive workflow summaries and publish log artifacts with the release notes if the run required manual follow-up.
