# ThreadPilot Pre-Tag Release Checklist

Use this checklist before creating a release tag. It is aligned with local build scripts and the GitHub release workflow.

## Release Meta

- [ ] Target version: 1.1.1
- [ ] Target tag: v1.1.1
- [ ] Branch is correct for release
- [ ] Working tree is clean (except intentionally ignored artifact files)

## 1) Artifact Cleanup (Local)

Goal: keep only 1.1.1 artifacts in artifacts/release.

- [ ] Delete old installer: artifacts/release/installer/ThreadPilot_v1.1.0_Setup.exe
- [ ] Delete old zips: artifacts/release/ThreadPilot_v1.1.0_Installer.zip, artifacts/release/ThreadPilot_v1.1.0_Portable.zip
- [ ] Delete temporary staging dirs: artifacts/release/package-stage, artifacts/release/_stage_installer, artifacts/release/_stage_portable

Expected state:

- [ ] artifacts/release/installer/ThreadPilot_v1.1.1_Setup.exe
- [ ] artifacts/release/packages/ThreadPilot_v1.1.1_Installer.zip
- [ ] artifacts/release/packages/ThreadPilot_v1.1.1_Portable.zip

## 2) Build Gates (Must Pass)

Run from repository root.

Commands:

  dotnet restore "ThreadPilot_1.sln"
  dotnet build "ThreadPilot_1.sln" --configuration Release --no-restore
  dotnet test "ThreadPilot_1.sln" --configuration Release --no-build

- [ ] Restore passed
- [ ] Build passed
- [ ] Tests passed

## 3) Packaging Gates (Local)

Commands:

  ./build/build-installer.ps1 -Version "1.1.1"
  ./build/package-release-zips.ps1 -Version "1.1.1"

- [ ] Installer generated in artifacts/release/installer
- [ ] Zip packages generated in artifacts/release/packages
- [ ] Inno Setup warnings target is zero (artifacts/release/installer_iscc_setup.log)

## 4) Naming Gate (Local vs CI)

Local script naming (package-release-zips.ps1):

- [ ] ThreadPilot_v1.1.1_Installer.zip
- [ ] ThreadPilot_v1.1.1_Portable.zip

GitHub workflow naming (release.yml package step):

- [ ] ThreadPilot_v1.1.1_singlefile_win-x64.zip
- [ ] ThreadPilot_v1.1.1_readytorun_win-x64.zip

Note: naming differs by channel (local script vs CI workflow). Validate the expected set for the channel you are releasing from.

## 5) Hash + Signature Gate

Generate and verify SHA256 hashes:

Commands:

    $hashFile = "artifacts/release/SHA256SUMS.txt"
    if (Test-Path $hashFile) { Remove-Item $hashFile -Force }

    $releaseFiles = @()
    $releaseFiles += Get-ChildItem "artifacts/release/packages" -File -ErrorAction SilentlyContinue
    $releaseFiles += Get-ChildItem "artifacts/release/installer/*.exe" -File -ErrorAction SilentlyContinue

    $releaseFiles | ForEach-Object {
      $hash = Get-FileHash $_.FullName -Algorithm SHA256
      "$($hash.Hash)  $($_.Name)" | Out-File -FilePath $hashFile -Append -Encoding utf8
    }

- [ ] SHA256SUMS.txt generated
- [ ] Every shipped artifact has one hash row

If signing is enabled:

- [ ] Authenticode signature is valid (Get-AuthenticodeSignature)
- [ ] Timestamp present (DigiCert or equivalent)

## 6) Smoke Test Installation

Portable smoke test:

Commands:

  $tmp = "$env:TEMP/ThreadPilot_Smoke_$(Get-Random)"
  Expand-Archive "artifacts/release/packages/ThreadPilot_v1.1.1_Portable.zip" -DestinationPath $tmp -Force
  Test-Path "$tmp/ThreadPilot.exe"

- [ ] Portable archive extracts correctly
- [ ] ThreadPilot.exe exists and starts

Installer smoke test:

- [ ] Run artifacts/release/installer/ThreadPilot_v1.1.1_Setup.exe
- [ ] Install completes without errors
- [ ] Start menu and/or desktop shortcut launches app
- [ ] Uninstall path works
- [ ] Theme is coherent on first launch (system dark -> dark UI + checked setting)

## 7) Pre-Tag Final Gate

- [ ] All gates above are green
- [ ] Release notes prepared
- [ ] Optional signing decision documented (signed/unsigned)

Create tag only after all checks pass:

Commands:

  git tag -a v1.1.1 -m "Release ThreadPilot v1.1.1"
  git push origin v1.1.1
