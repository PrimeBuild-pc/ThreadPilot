# Packaging and Distribution Guide

This document describes production packaging for ThreadPilot v1.1.0.

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

### 1) Self-contained Single-file

```powershell
dotnet publish ThreadPilot.csproj -c Release -p:PublishProfile=WinX64-SingleFile
```

Output folder:

- `artifacts/release/singlefile/`

### 2) ReadyToRun (folder deployment)

```powershell
dotnet publish ThreadPilot.csproj -c Release -p:PublishProfile=WinX64-ReadyToRun
```

Output folder:

- `artifacts/release/readytorun/`

### 3) MSIX package

```powershell
dotnet publish ThreadPilot.csproj -c Release -p:PublishProfile=WinX64-MSIX
```

Output:

- `artifacts/release/msix/ThreadPilot_<version>_win-x64.msix`
- `artifacts/release/msix/publish/` (packaging input folder used to create the MSIX)

`WinX64-MSIX.pubxml` enables a post-publish packaging target that calls `build-msix.ps1` and creates the `.msix` using the Windows SDK `makeappx.exe` tool.

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

Use Inno Setup script (`setup.iss`) for legacy installer packaging.

```powershell
iscc setup.iss
```

## Recommended Release Sequence

1. `dotnet restore`
2. `dotnet build ThreadPilot_1.sln --configuration Release`
3. `dotnet test ThreadPilot_1.sln --configuration Release --no-build`
4. Publish all profiles (Single-file, ReadyToRun, MSIX)
5. Sign artifacts
6. Generate SHA-256 checksums
7. Upload release artifacts

## CI/CD

The release workflow (`.github/workflows/release.yml`) now builds:

- Single-file package (ZIP)
- ReadyToRun package (ZIP)
- MSIX artifact
- SHA-256 checksum manifest
- Optional signing of EXE/MSIX artifacts when signing secrets are configured
- Explicit MSIX validation (workflow fails if no package artifact is generated)

