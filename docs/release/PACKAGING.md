# Packaging and Distribution Guide

This document describes the current production packaging and distribution contract for ThreadPilot releases.

## Prerequisites

- Windows 11 build machine (or GitHub Actions `windows-latest`)
- .NET SDK 8.x
- Optional for signing:
- Code-signing certificate (`.pfx`) for Authenticode signing
- Optional strong-name key (`.snk`) if strong naming is required

## Publish Profiles

The repository provides two publish profiles under `Properties/PublishProfiles`:

- `WinX64-SingleFile.pubxml`
- `WinX64-ReadyToRun.pubxml`

Shared packaging defaults are in `Directory.Publish.props`.

## Build Variants

Release baseline requirements:

- Executable manifest must use requireAdministrator so portable and installed builds always launch elevated.
- Inno Setup compile warnings target: 0.
- Build and test stages must pass before packaging.

### 1) Self-contained Single-file

```powershell
dotnet publish ThreadPilot.csproj -c Release -p:PublishProfile=WinX64-SingleFile
```

Output folder:

- `artifacts/release/singlefile/`

### 2) Inno Setup Installer (primary)

```powershell
iscc /DMyAppVersion=<version> /DMyAppSourceDir="..\\artifacts\\release\\singlefile" Installer/setup.iss
```

Recommended local command (prevents stale publish output by forcing a fresh publish before ISCC):

```powershell
powershell -ExecutionPolicy Bypass -File build/build-installer.ps1 -Version <version>
```

The installer now uses Inno Setup native dynamic theming (`WizardStyle=modern dynamic windows11`) and follows the current Windows light/dark preference at startup.

Optional override for QA/troubleshooting:

```powershell
iscc /DMyWizardStyle="modern dark windows11" /DMyAppVersion=<version> /DMyAppSourceDir="..\\artifacts\\release\\singlefile" Installer/setup.iss
```

Output folder:

- `artifacts/release/installer/`

### 3) ReadyToRun (folder deployment)

```powershell
dotnet publish ThreadPilot.csproj -c Release -p:PublishProfile=WinX64-ReadyToRun
```

Output folder:

- `artifacts/release/readytorun/`

## Code Signing

### Strong name (optional)

1. Generate key:

```powershell
sn -k ThreadPilot.snk
```

2. Add to project settings:

- `<SignAssembly>true</SignAssembly>`
- `<AssemblyOriginatorKeyFile>ThreadPilot.snk</AssemblyOriginatorKeyFile>`

### Authenticode (recommended for release)

Sign binaries after publish:

```powershell
signtool sign /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 /f path\to\cert.pfx /p <password> artifacts\release\singlefile\ThreadPilot.exe
```

## CI Signing Placeholders

`.github/workflows/release.yml` supports optional signing with these repository secrets:

- `WINDOWS_SIGNING_CERT_BASE64`: base64-encoded `.pfx` certificate
- `WINDOWS_SIGNING_CERT_PASSWORD`: certificate password

Expose these values to the workflow as environment variables (same names) via repository/environment secrets.

When both secrets are present:

1. The workflow decodes the certificate to the runner temp directory.
2. `signtool.exe` signs `.exe` artifacts before ZIP packaging.
3. SHA-256 checksums are generated for signed outputs.

When secrets are missing, the release still builds and publishes unsigned artifacts.

## Classic Installer

Use Inno Setup script (`Installer/setup.iss`) for primary end-user installer packaging.

```powershell
iscc Installer/setup.iss
```

## Recommended Release Sequence

1. `dotnet restore`
2. `dotnet build ThreadPilot_1.sln --configuration Release`
3. `dotnet test Tests/ThreadPilot.Core.Tests/ThreadPilot.Core.Tests.csproj --configuration Release --collect:"XPlat Code Coverage" --settings "Tests/ThreadPilot.Core.Tests/coverlet.runsettings" --results-directory TestResults`
4. Publish Single-file and ReadyToRun profiles
5. Build Inno Setup installer from Single-file output
6. Generate winget manifests from release metadata
7. Sign artifacts
8. Generate SHA-256 checksums
9. Upload release artifacts

## Release Asset Contract

## Release Asset Contract

The release workflow publishes these public GitHub release assets:

- `ThreadPilot_v<version>_Setup.exe` (Inno Setup installer)
- `ThreadPilot_v<version>_Portable.zip` (portable deployment)
- `SHA256SUMS.txt` (checksums for public assets)

ReadyToRun and single-file build outputs are internal CI validation artifacts only. They are not uploaded as public GitHub release assets.

Generated winget manifest YAML files and `manifest.spdx.json` are internal workflow artifacts only. They are retained for channel submission and release provenance, but they are not uploaded as public GitHub release assets.

Prefer the release page over hardcoded asset URLs:

- `https://github.com/PrimeBuild-pc/ThreadPilot/releases/latest`

## Package Channel Alignment

Use GitHub release as the source of truth for version alignment:

- Publish GitHub release `vX.Y.Z` first.
- Ensure winget/chocolatey metadata references the same `X.Y.Z`.
- Ensure package URLs point to assets under the same GitHub tag.

Channel behavior:

- Chocolatey packages can be temporarily hidden from normal search while moderation/verification is pending or failed.
- Winget packages are discoverable only after the manifest is accepted in microsoft/winget-pkgs and clients refresh sources.

Current workflow scope:

- `.github/workflows/release.yml` builds release artifacts, generates winget manifests from the version/tag/installer URL/SHA, and uploads those manifests as artifacts.
- It automatically submits a PR to `microsoft/winget-pkgs` when release succeeds and winget secrets are configured.
- `.github/workflows/publish-chocolatey.yml` validates Chocolatey packaging and can publish to the Chocolatey community feed, but it only runs through manual `workflow_dispatch`.
- Tagged public releases fail if winget publication secrets are missing; only manual `workflow_dispatch` dry-runs may skip winget publication.
- Chocolatey publication is intentionally decoupled from tagged public releases because older package versions can remain in Chocolatey moderation. Run it manually only after previous ThreadPilot versions clear moderation.

## winget Manifest Generation

The release workflow no longer depends on a precommitted folder under `winget/manifests/.../<version>`.

Generate manifests locally with:

```powershell
./build/generate-winget-manifests.ps1 `
  -Version "1.4.0" `
  -Tag "v1.4.0" `
  -InstallerUrl "https://github.com/PrimeBuild-pc/ThreadPilot/releases/download/v1.4.0/ThreadPilot_v1.4.0_Setup.exe" `
  -InstallerSha256 "<sha256>" `
  -OutputRoot "winget-manifests"
```

Expected output:

- `winget-manifests/PrimeBuild.ThreadPilot.yaml`
- `winget-manifests/PrimeBuild.ThreadPilot.locale.en-US.yaml`
- `winget-manifests/PrimeBuild.ThreadPilot.installer.yaml`

Validation checklist:

- files exist
- `PackageIdentifier` is `PrimeBuild.ThreadPilot`
- `PackageVersion` matches the release version
- installer URL and SHA are populated

Required repository secrets for full channel automation:

- `WINGET_GITHUB_TOKEN`: PAT with permission to push to your `winget-pkgs` fork and create PRs.
- `WINGET_FORK_OWNER`: GitHub username/org that owns your `winget-pkgs` fork.
- `CHOCOLATEY_API_KEY`: API key for `https://push.chocolatey.org/`. Required only by the manual Chocolatey workflow when `publish=true`.

If winget secrets are missing on a tagged public release, the release workflow fails by policy instead of staying ambiguously green. Missing Chocolatey credentials do not block GitHub release creation.

## Chocolatey Validation and Publish

Chocolatey publishing is manual. Use it after the GitHub release exists and after previous ThreadPilot package versions have cleared Chocolatey moderation.

Manual workflow:

1. Open GitHub Actions.
2. Select `Publish Chocolatey`.
3. Run workflow with `tag=vX.Y.Z`.
4. Use `publish=false` to validate/package only.
5. Use `publish=true` to push to Chocolatey, with `CHOCOLATEY_API_KEY` configured.

The workflow downloads `ThreadPilot_vX.Y.Z_Setup.exe` from the existing GitHub release, runs `build/publish-chocolatey.ps1` in dry-run mode first, and uploads `.nupkg`, metadata JSON, and logs as workflow artifacts.

Local packaging-only validation:

```powershell
./build/publish-chocolatey.ps1 `
  -Version "1.4.0" `
  -Tag "v1.4.0" `
  -InstallerPath ".\artifacts\release\installer\ThreadPilot_v1.4.0_Setup.exe" `
  -DryRun `
  -PackageOutputDirectory ".\artifacts\choco-dryrun" `
  -MetadataOutputPath ".\artifacts\choco-dryrun\chocolatey-package-metadata.json"
```

Expected outputs:

- `threadpilot.<version>.nupkg`
- `chocolatey-package-metadata.json`

The metadata JSON captures:

- resolved installer URL
- installer SHA256
- package path
- release notes URL
- whether publication was attempted
- dry-run / no-push result

Public publish path:

```powershell
./build/publish-chocolatey.ps1 `
  -Version "1.4.0" `
  -Tag "v1.4.0" `
  -InstallerPath ".\artifacts\release\installer\ThreadPilot_v1.4.0_Setup.exe" `
  -ApiKey "<chocolatey-api-key>"
```

Moderation note:

- A successful `choco push` means the package was submitted to Chocolatey.
- It does not guarantee immediate discoverability in search results.
- Moderation delay is an external queue, not automatically evidence of a repo or workflow failure.
- Do not run the manual publish workflow for a new version while prior ThreadPilot versions are still blocked or pending in Chocolatey moderation.

Optional automation for publishing the GitHub release after artifacts are ready:

```powershell
./build/create-github-release.ps1 -Version "<version>" -NotesFile "docs/release/RELEASE_NOTES.md"
```

## CI/CD

The release workflow (`.github/workflows/release.yml`) now builds:

- Inno Setup installer (`.exe`) as primary installer artifact
- Portable package (ZIP) as public distribution
- ReadyToRun build output for internal CI validation only
- SHA-256 checksum manifest for public assets
- Optional signing of EXE artifacts when signing secrets are configured
