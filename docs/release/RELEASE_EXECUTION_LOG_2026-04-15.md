# Release Execution Log

Date: 2026-04-15
Scope: Final one-shot packaging execution after completion of non-packaging phases.

## Commands Executed

1. build/build-installer.ps1 -Version 1.1.1
2. dotnet publish ThreadPilot.csproj --configuration Release -p:PublishProfile=WinX64-MSIX
3. build/package-release-zips.ps1 -Version 1.1.1
4. SHA256 manifest generation to artifacts/release/SHA256SUMS.txt

## Result

- Installer build: success
- MSIX publish: success
- Release ZIP packaging: success
- Checksum generation: success

## Produced Artifacts

- artifacts/release/installer/ThreadPilot_v1.1.1_Setup.exe
- artifacts/release/msix/ThreadPilot_1.1.1.0_win-x64.msix
- artifacts/release/packages/ThreadPilot_v1.1.1_Portable.zip
- artifacts/release/packages/ThreadPilot_v1.1.1_Installer.zip
- artifacts/release/SHA256SUMS.txt

## ZIP Hashes (from packaging output)

- ThreadPilot_v1.1.1_Portable.zip
  - SHA256: F53B0203AFF269C092143B297F1C16D2AAD638B5EB0FB127BC4CA9416591BD5C
- ThreadPilot_v1.1.1_Installer.zip
  - SHA256: EBF10BFE4C39317A9CDC41DEE9BA9E80BC10961D887797EC5D824B83DD9440A5
