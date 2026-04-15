# Packaging and Distribution Guide

This document describes production packaging for ThreadPilot v1.1.1.

## Prerequisites

- Windows 11 build machine (or GitHub Actions `windows-latest`)
- .NET SDK 8.x
- Optional for signing:
- Code-signing certificate (`.pfx`) for Authenticode/MSIX signing
- Optional strong-name key (`.snk`) if strong naming is required

## Publish Profiles

The repository provides three publish profiles under `Properties/PublishProfiles`:

- `WinX64-SingleFile.pubxml`
- `WinX64-ReadyToRun.pubxml`
- `WinX64-MSIX.pubxml`

Shared packaging defaults are in `Directory.Publish.props`.

## Build Variants

Release baseline requirements:

- Executable manifest must use requireAdministrator.
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
iscc /DMyAppVersion=1.1.1 /DMyAppSourceDir="..\\artifacts\\release\\singlefile" Installer/setup.iss
```

Recommended local command (prevents stale publish output by forcing a fresh publish before ISCC):

```powershell
powershell -ExecutionPolicy Bypass -File build/build-installer.ps1 -Version 1.1.1
```

The installer now uses Inno Setup native dynamic theming (`WizardStyle=modern dynamic windows11`) and follows the current Windows light/dark preference at startup.

Optional override for QA/troubleshooting:

```powershell
iscc /DMyWizardStyle="modern dark windows11" /DMyAppVersion=1.1.1 /DMyAppSourceDir="..\\artifacts\\release\\singlefile" Installer/setup.iss
```

Output folder:

- `artifacts/release/installer/`

### 3) ReadyToRun (folder deployment)

```powershell
dotnet publish ThreadPilot.csproj -c Release -p:PublishProfile=WinX64-ReadyToRun
```

Output folder:

- `artifacts/release/readytorun/`

### 4) MSIX package (secondary)

```powershell
dotnet publish ThreadPilot.csproj -c Release -p:PublishProfile=WinX64-MSIX
```

Output:

- `artifacts/release/msix/ThreadPilot_<version>_win-x64.msix`
- `artifacts/release/msix/publish/` (packaging input folder used to create the MSIX)

`WinX64-MSIX.pubxml` enables a post-publish packaging target that calls `build/build-msix.ps1` and creates the `.msix` using the Windows SDK `makeappx.exe` tool.

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

For MSIX signing:

- Sign the produced `.msix`/`.appx` using `signtool` with the same certificate.
- Keep `AppxPackageSigningEnabled=false` in profile and sign in CI so secrets are not stored in project files.

## CI Signing Placeholders

`.github/workflows/release.yml` supports optional signing with these repository secrets:

- `WINDOWS_SIGNING_CERT_BASE64`: base64-encoded `.pfx` certificate
- `WINDOWS_SIGNING_CERT_PASSWORD`: certificate password

Expose these values to the workflow as environment variables (same names) via repository/environment secrets.

When both secrets are present:

1. The workflow decodes the certificate to the runner temp directory.
2. `signtool.exe` signs `.exe` and MSIX/AppX artifacts before ZIP packaging.
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
3. `dotnet test ThreadPilot_1.sln --configuration Release --no-build`
4. Publish Single-file and ReadyToRun profiles
5. Build Inno Setup installer from Single-file output
6. Publish MSIX as secondary artifact
7. Sign artifacts
8. Generate SHA-256 checksums
9. Upload release artifacts

## CI/CD

The release workflow (`.github/workflows/release.yml`) now builds:

- Inno Setup installer (`.exe`) as primary installer artifact
- Single-file package (ZIP)
- ReadyToRun package (ZIP)
- MSIX artifact (secondary)
- SHA-256 checksum manifest
- Optional signing of EXE/MSIX artifacts when signing secrets are configured
- Explicit MSIX validation (workflow fails if no package artifact is generated)

