# ThreadPilot: Microsoft Store MSIX build

This is a first certification candidate with the current administrative features. Building, schema validation and local signatures do not establish Store approval or successful deployment. No upload or submission is part of this script.

## Identity and support

Read from [Partner Center Product Identity](https://partner.microsoft.com/en-us/dashboard/products/9PH5QDX08R6Z/identity) on 2026-11-16:

- Package name: `LorenzoMassafra.ThreadPilot`
- Publisher: `CN=ACD03D0F-7A52-4C93-ADF8-A07702180E72`
- Publisher display name: **Prime Build**
- Package family: `LorenzoMassafra.ThreadPilot_2j208vxf88tt6`
- Product ID: `9PH5QDX08R6Z`
- Public support: primebuild.official@gmail.com · https://primebuild.website (bug reports also on [GitHub Issues](https://github.com/PrimeBuild-pc/ThreadPilot/issues)).

Technical identity strings are assigned by Microsoft and are preserved. Do not replace them with the public brand name.

## Rebuild

Run in Windows PowerShell 5.1 on Windows 11, with .NET SDK **8.0.428** and Windows SDK **10.0.26100.0** (x64 MakeAppx, SignTool and mt.exe). The currently installed MakeAppx file version is 10.0.26100.9457. `identity.json` pins toolchain/runtime versions; `packages.lock.json` pins NuGet dependency versions and content hashes.

```powershell
Set-Location 'C:\Users\Lorenzo\Documents\Projects\ThreadPilot'
powershell -NoProfile -ExecutionPolicy Bypass -File .\packaging\msix\Build-Msix.ps1 -SignForTest
```

Omit `-SignForTest` to produce only the unsigned Store artifact. Use `-OutputDirectory <path>` for a separate rebuild. `-UpdateLockFile` is only for an intentional dependency/toolchain update; normal builds enforce locked restore. The package version is the stable project version plus `.0` (the Store reserves the last component).

This is Release, win-x64, self-contained .NET 8.0.34, untrimmed, with separate native/runtime files, no ReadyToRun or native self-extraction. No machine-wide .NET or Windows App SDK installation is required. Windows 11 build 22000 is the minimum. `MaxVersionTested=10.0.26100.0` is manifest metadata, not evidence of an OS test in this session.

Output: `artifacts/msix/1.7.11.0/`, containing `*-Store.msix`, optional `*-Test.msix`, public `*-Test.cer`, `SHA256SUMS.txt` and a build receipt. These are actual MakeAppx-generated MSIX files; no installer is renamed and no sparse/external-location package is used. A plain `.msix` is a supported submission artifact; `.msixupload` is not generated.

Two independent builds were compared: all uncompressed entries, including executable bytes, runtime/native DLLs, manifest, icons and block map, match. MakeAppx writes current ZIP timestamps, so the container hash changes between runs even after input timestamp normalization. The build is reproducible in payload, **not bit for bit at container level**. Test signatures additionally change with each generated certificate/signing time. Do not expect the delivered SHA-256 after a fresh build.

## Capabilities and Microsoft approval

`runFullTrust`, `allowElevation`, `unvirtualizedResources` are explicitly declared. The app runs as a packaged classic desktop app. `mediumIL` describes the full-trust app type; it does not cancel the executable's UAC manifest or runtime elevation requests.

- `runFullTrust`: required for the existing desktop application and child-process operations.
- `allowElevation`: preserves the app's current launch/runtime elevation. Microsoft requires advance justification and approval under strict criteria. Follow the contact route in the official capability documentation; no approval or contact has been made by this build.
- `unvirtualizedResources`: both RegistryWriteVirtualization and FileSystemWriteVirtualization are disabled. HKCU tweaks must affect the actual operating system rather than a private package hive; state and undo files must be visible to the existing elevated/non-elevated child processes and survive uninstall. This is another restricted capability requiring review; Microsoft explicitly warns approval is uncommon. Do not remove it merely to hide real behavior.

System changes and unvirtualized data can persist after package removal. Open **Settings → Clean up before uninstalling** in ThreadPilot, review the cleanup report, then remove the package through Windows Settings. The in-app command shuts down monitoring and Game Boost, removes its verified startup task, restores eligible recorded changes and offers to remove external per-user data.

## Channel-specific behavior

- The embedded EXE manifest still requires `requireAdministrator` at startup.
- Settings, profiles, configuration, masks, undo data and power plans use `%APPDATA%\ThreadPilot`; logs use `%LOCALAPPDATA%\ThreadPilot\Logs`. The Store build never chooses a power-plan folder inside its immutable payload.
- Existing migration sources beside the executable are read-only inputs; no user data is included in the MSIX.
- The Store build creates one task, `ThreadPilot_Store_Startup`, and only while "Start with Windows" is on (off for new installs, in every channel). It is **not elevated** (`/RL LIMITED`) and runs `%windir%\explorer.exe shell:AppsFolder\<package family name>!App`: normal package activation, so the app gets package identity and Windows shows the UAC prompt at sign-in. A packaged exe started by path has no identity and can fail to load files MSIX shares between packages (seen as `FileNotFoundException: WinRT.Runtime`). The UAC-free `ThreadPilot_Store_Launch` task is never created by the Store build; a leftover one is deleted. The EXE channel keeps its own `ThreadPilot_Launch`/`ThreadPilot_Startup`.
- The activation target (package family name and app ID) does not change across updates. Each elevated start with autostart on re-registers the startup task if it is missing or still in the older run-the-exe-by-path form. A sideloaded update 1.7.11.0 → 1.7.12.0 was exercised on a host; a Store-delivered update has not been. Custom package volumes outside Program Files are not supported by the existing protected-path check.
- The existing EXE-channel settings are shared. Back them up before testing; do not run the EXE and MSIX simultaneously. Cleanup retains shared data and Windows changes when both installed channels are detected.
- All GitHub update checks and EXE installer execution are blocked by the Store channel, including a guard in the installer service itself.

The EXE/portable build defaults and existing installer workflows are preserved; Store behavior is selected only with `StorePackage=true` / `MSIX_STORE`.

## Signing and validation

The `*-Store.msix` artifact is intentionally unsigned. Microsoft signs Store-distributed MSIX packages; no paid signing service is required for this path. Only upload the Store artifact in a later authorized session.

The optional test copy uses a one-year, self-signed RSA/SHA-256 code-signing certificate with the exact Publisher subject. The build creates it temporarily in **CurrentUser\My**, never in host trust stores. Its non-exportable private key and certificate are deleted in `finally`; no PFX/private key is delivered or stored in the repo. Only the public CER is exported. The test signature has no timestamp and is usable only while the certificate is valid. Trust the CER **only inside the disposable VM**.

MakeAppx runs with schema/semantic validation enabled. `mt.exe` checks the embedded EXE launch level, and the build compares file/package/runtime versions. `Test-Package.ps1` verifies identity, display name, architecture, capability declarations, unvirtualized writes, executable/launch asset presence, icon dimensions, self-contained dependencies and every SHA-256 block in AppxBlockMap. For test packages it verifies the CMS signature and checks the Windows APPX SIP result; an untrusted-root result is expected on the host. This is not a full Windows App Certification Kit run or installation test.

```powershell
.\packaging\msix\Test-Package.ps1 -PackagePath .\artifacts\msix\1.7.11.0\ThreadPilot_1.7.11.0_x64-Store.msix
.\packaging\msix\Test-Package.ps1 -PackagePath .\artifacts\msix\1.7.11.0\ThreadPilot_1.7.11.0_x64-Test.msix -CertificatePath .\artifacts\msix\1.7.11.0\ThreadPilot_1.7.11.0_x64-Test.cer
dotnet test .\Tests\ThreadPilot.Core.Tests\ThreadPilot.Core.Tests.csproj -c Release --filter 'FullyQualifiedName!~StorePackageTests'
dotnet test .\Tests\ThreadPilot.Core.Tests\ThreadPilot.Core.Tests.csproj -c Release -p:StorePackage=true --filter 'FullyQualifiedName~StorePackage'
```

Recorded results (1.7.11): 969 EXE-channel tests and 6 Store-channel tests passed. ThreadPilot emits existing analyzer/style warnings; consult the build log. VM launch/UAC/data/tweak round trips, lifecycle updates, uninstall and full WACK remain to be verified in the shared VM by the original chat.

## Microsoft references

- [Generate desktop MSIX components](https://learn.microsoft.com/en-us/windows/msix/desktop/desktop-to-uwp-manual-conversion)
- [MakeAppx and its limited validation](https://learn.microsoft.com/en-us/windows/msix/package/create-app-package-with-makeappx-tool)
- [Restricted capabilities, including elevation](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/app-capability-declarations)
- [Virtualization and persistent system-visible state](https://learn.microsoft.com/en-us/windows/msix/desktop/flexible-virtualization)
- [Store signing and local test signing](https://learn.microsoft.com/en-us/windows/msix/package/signing-package-overview)
