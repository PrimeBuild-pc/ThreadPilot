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

Use script automation (requires authenticated gh CLI):

```powershell
./build/create-github-release.ps1 -Version "<version>" -NotesFile "docs/release/RELEASE_NOTES.md"
```

Manual fallback:

1. Create tag: `git tag -a v<version> -m "Release v<version>"`
2. Push tag: `git push origin v<version>`
3. Create GitHub release and upload artifacts.

## Post-Release (T+24h)

1. Verify downloadable artifacts are accessible.
2. Execute smoke installation on clean environment.
3. Monitor issue tracker for regressions.
4. Document hotfix decisions if needed.
