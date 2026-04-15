# ThreadPilot — Piano di Miglioramento Completo

## Context
ThreadPilot è un'app WPF/.NET 8 open-source (AGPL v3) per Windows 11, alternativa a Process Lasso. L'obiettivo è renderla più robusta, sicura, performante e professionale come repository pubblica distribuita via GitHub Releases, winget e Chocolatey. Non è disponibile un certificato di firma codice.

---

## SEZIONE 1 — BUG CRITICI / RESOURCE LEAK

### 1.1 Race condition WMI EventWatcher + disposed flag
**File:** `Services/ProcessMonitorService.cs`
- Sposta `this.disposed = true` **prima** di iniziare il teardown (attualmente riga ~675 è dopo).
- Usa `Interlocked.Exchange(ref this.disposedFlag, 1)` invece di `bool`.
- Nel callback `FallbackPollingCallback` aggiungi guard: `if (Interlocked.CompareExchange(ref this.disposedFlag, 0, 0) == 1) return;`

### 1.2 SemaphoreSlim non rilasciato su eccezione
**File:** `Services/ProcessMonitorService.cs:39`
- Wrappa il blocco in `StopMonitoringAsync` con `try/finally { wmiStartSemaphore.Release(); }` se il semaphore è held.
- Aggiungi `wmiStartSemaphore.Dispose()` in `Dispose()` **dopo** che il flag disposed è settato.

### 1.3 PerformanceCounter partial-init leak
**File:** `Services/PerformanceMonitoringService.cs` — `InitializeCpuCoreCounters()`
- Accumula contatori in lista temporanea `tempCounters`, assegna a `cpuCoreCounters` solo se il loop completa senza eccezioni.
- Nel `catch`: disponi tutti i contatori in `tempCounters` prima di propagare.

### 1.4 Process handle leak in CreateProcessModel
**File:** `Services/ProcessService.cs:136`
- `ProcessModel` non deve mantenere un riferimento `Process process` vivo. Estrai tutti i campi necessari (Id, Name, Priority, Affinity, MemoryUsage, CPU%) immediatamente e **non** salvare il riferimento `Process` nel modello.
- Se il modello deve riattivare la `Process`, ricreala on-demand con `Process.GetProcessById(pid)` wrappato in try-catch (può già essere terminato).

---

## SEZIONE 2 — PERFORMANCE

### 2.1 Dictionary allocation nel polling fallback
**File:** `Services/ProcessMonitorService.cs:452-454`
- Sostituisci `currentProcesses.ToDictionary(...)` con un `Dictionary<int, ProcessModel>` riutilizzato come campo privato: `this.pollBuffer.Clear(); foreach (var p in currentProcesses) this.pollBuffer[p.ProcessId] = p;`

### 2.2 WMI query senza timeout
**File:** `Services/PerformanceMonitoringService.cs:179,193,394`
- Per ogni `ManagementObjectSearcher` aggiungi scope con timeout:
  ```csharp
  var options = new ConnectionOptions { Timeout = TimeSpan.FromSeconds(5) };
  var scope = new ManagementScope(@"\\.\root\cimv2", options);
  scope.Connect();
  using var searcher = new ManagementObjectSearcher(scope, new ObjectQuery("..."));
  ```

### 2.3 Ridurre Process.GetProcesses() calls
**File:** `Services/PerformanceMonitoringService.cs:110`, `Services/ProcessService.cs:70,528,551,564`
- Per il solo conteggio processi attivi usa: `int count = (int)new ManagementObjectSearcher("SELECT COUNT(*) FROM Win32_Process").Get().Cast<ManagementBaseObject>().First()["Count"];` con cache di 5s.
- Per l'enumerazione completa in `ProcessService`, mantenere, ma assicurarsi che sia chiamata solo su richiesta esplicita dell'UI (trigger manuale o timer UI, non loop interni).

### 2.4 Historical data memory: List → CircularBuffer
**File:** `Services/PerformanceMonitoringService.cs:119-126`
- Sostituisci `List<SystemPerformanceMetrics>` con una `Queue<SystemPerformanceMetrics>` di capacità fissa (1000):
  ```csharp
  if (this.historicalData.Count >= 1000) this.historicalData.Dequeue();
  this.historicalData.Enqueue(metrics);
  ```
- `RemoveAt(0)` su List è O(n); `Dequeue()` su Queue è O(1).

### 2.5 StackTrace unbounded nel logging
**File:** `Models/LogEventTypes.cs:203`
- Tronca: `(exception.StackTrace ?? "N/A")[..Math.Min(2000, exception.StackTrace?.Length ?? 0)]`

---

## SEZIONE 3 — SICUREZZA

### 3.1 Protected process list incompleta
**File:** `Services/SecurityService.cs:50-62`
- Aggiungi alla denylist statica: `"WmiPrvSE"`, `"MsMpEng"`, `"SecurityHealthService"`, `"audiodg"`.
- Implementa controllo dinamico via Windows API: chiama `IsProtectedProcess` tramite P/Invoke su `ntdll.dll` oppure controlla il flag `PROCESS_CREATION_PROTECTED_PROCESS` via `NtQueryInformationProcess`. Inserisci il risultato nella `SecurityService.IsProtected(Process p)` come check supplementare al denylist statico.

### 3.2 Unused constant (code cleanup)
**File:** `Services/SecurityService.cs:31`
- Rimuovi `private const int MaxLogTokenLength = 200;` (non utilizzata).

---

## SEZIONE 4 — QUALITÀ DEL CODICE

### 4.1 MainWindow.xaml.cs troppo grande (1935 righe)
**File:** `MainWindow.xaml.cs`
- Estrai la logica delle tab/navigazione in un `NavigationBehavior` (attached behavior WPF).
- Estrai la logica DWM dark mode in una classe statica `DwmHelper` in `Helpers/`.
- Estrai la gestione della System Tray in `SystemTrayService` (già esiste, consolida lì).
- Obiettivo: ridurre il code-behind sotto 400 righe.

### 4.2 ProcessViewModel mixed concerns (1800+ righe)
**File:** `ViewModels/ProcessViewModel.cs`
- Estrai la logica di filtering/sorting in una classe `ProcessFilterService` o nested helper `ProcessFilter`.
- Estrai la logica di refresh throttling in una classe `ThrottledRefreshCoordinator` riutilizzabile.

### 4.3 ConfigureAwait(false) mancante
- In tutti i metodi `async` nei **Service layer** (non ViewModel, non codebehind) aggiungi `.ConfigureAwait(false)` su ogni `await` che non richiede il contesto UI. Questo vale per: `ProcessMonitorService`, `PerformanceMonitoringService`, `ProcessService`, `PowerPlanService`, `ConditionalProfileService`, `ProcessPowerPlanAssociationService`.

### 4.4 Empty catch senza logging
**File:** `Services/ProcessMonitorService.cs:579`
- Sostituisci il catch vuoto con:
  ```csharp
  catch (Exception ex)
  {
      this.logger?.LogDebug(ex, "Process {ProcessId} terminated before access", processId);
      return null;
  }
  ```

### 4.5 Hardcoded PerformanceCounter category names
**File:** `Services/PerformanceMonitoringService.cs:80-81`
- Wrappa la creazione di ogni `PerformanceCounter` in try-catch con log Error e re-throw, per evitare fallimenti silenziosi su sistemi localizzati.
- Aggiungi `.NextValue()` subito dopo la creazione come "prime" del contatore.

---

## SEZIONE 5 — DISTRIBUZIONE (winget / Chocolatey)

### 5.1 Winget manifest
**Crea:** `winget/manifests/p/PrimeBuild/ThreadPilot/1.1.1/`
- `PrimeBuild.ThreadPilot.yaml` (installer manifest YAML v1.4.0)
- `PrimeBuild.ThreadPilot.locale.en-US.yaml`
- `PrimeBuild.ThreadPilot.installer.yaml`
- Campi obbligatori: PackageIdentifier, PackageVersion, PackageLocale, Publisher, PackageName, License, ShortDescription, InstallerType (inno), InstallerUrl, InstallerSha256.
- InstallerUrl punterà al `.exe` della GitHub Release corrispondente.
- Aggiungere uno step nel workflow `release.yml` che genera l'hash SHA256 dell'installer e lo inserisce nel manifest automaticamente.

### 5.2 Chocolatey package
**Crea:** `chocolatey/`
- `threadpilot.nuspec` — metadata del pacchetto (id, version, authors, description, tags, licenseUrl, projectUrl, iconUrl, releaseNotes)
- `tools/chocolateyInstall.ps1` — scarica l'installer da GitHub release e lo lancia in silent mode (`/VERYSILENT /SUPPRESSMSGBOXES /NORESTART`)
- `tools/chocolateyUninstall.ps1` — chiama l'uninstaller registrato in Windows
- `tools/LICENSE.txt` — copia della licenza AGPL v3

### 5.3 Aggiornare README con istruzioni di installazione
**File:** `README.md`
- Aggiungi sezione "Installation" con:
  ```
  winget install PrimeBuild.ThreadPilot
  choco install threadpilot
  ```
  e link al .exe dell'ultima release con istruzioni per verificare SHA256.

---

## SEZIONE 6 — REPOSITORY PROFESSIONALE (GitHub)

### 6.1 Aggiungere global.json
**Crea:** `global.json` nella root
```json
{ "sdk": { "version": "8.0.400", "rollForward": "latestFeature" } }
```

### 6.2 SBOM (Software Bill of Materials)
**File:** `.github/workflows/release.yml`
- Aggiungi step post-build:
  ```yaml
  - name: Generate SBOM
    run: dotnet tool install --global Microsoft.Sbom.DotNetTool && sbom-tool generate -b ./artifacts -bc . -pn ThreadPilot -pv ${{ steps.version.outputs.version }} -ps PrimeBuild -nsb https://github.com/PrimeBuild-pc/ThreadPilot
  - name: Upload SBOM
    uses: actions/upload-artifact@v4
    with:
      name: sbom
      path: _manifest/spdx_2.2/manifest.spdx.json
  ```

### 6.3 Code coverage report
**File:** `.github/workflows/ci-devsecops.yml`
- Dopo `dotnet test`, aggiungi upload a Codecov:
  ```yaml
  - name: Upload coverage
    uses: codecov/codecov-action@v4
    with:
      files: '**/coverage.cobertura.xml'
      fail_ci_if_error: false
  ```
- Aggiungi badge di coverage nel `README.md`.
- Nel `ThreadPilot.Core.Tests.csproj` aggiungi: `<CoverletOutputFormat>cobertura</CoverletOutputFormat>` e `<Threshold>40</Threshold>` (soglia realistica iniziale).

### 6.4 Smoke test nel workflow release
**File:** `.github/workflows/release.yml`
- Aggiungi job `smoke-test` dopo il build:
  ```yaml
  smoke-test:
    runs-on: windows-latest
    needs: build
    steps:
      - uses: actions/download-artifact@v4
        with: { name: release-portable }
      - name: Smoke test
        run: |
          Start-Process -FilePath ".\ThreadPilot.exe" -ArgumentList "--smoke-test" -Wait -PassThru | ForEach-Object { if ($_.ExitCode -ne 0) { exit 1 } }
  ```
  Implementa nel codice un flag `--smoke-test` in `App.xaml.cs` che inizializza i servizi e esce con codice 0.

### 6.5 Automated changelog
**File:** `.github/workflows/release.yml`
- Aggiungi step con `git-cliff` o GitHub's auto-generated release notes:
  ```yaml
  - name: Generate changelog
    uses: orhun/git-cliff-action@v3
    with:
      config: cliff.toml
      args: --latest
    id: git-cliff
  ```
- **Crea:** `cliff.toml` nella root con configurazione conventional commits (feat, fix, perf, docs, chore).

### 6.6 Branch protection documentation
**Crea:** `docs/CONTRIBUTING.md` — aggiungi sezione "Branch Policy":
- `main` richiede: 1 review, tutti i check CI verdi, no direct push.
- Naming convention branch: `feat/`, `fix/`, `perf/`, `chore/`, `release/`.
- Commit style: Conventional Commits (`feat: ...`, `fix: ...`, `perf: ...`).

### 6.7 CONTRIBUTORS.md
**Crea:** `CONTRIBUTORS.md` nella root
- Semplice lista markdown con nome/GitHub handle dei contributor.
- Aggiungi step nel `release.yml` per aggiornare automaticamente con `all-contributors-cli` o mantenuto manualmente.

### 6.8 Architectural Decision Records
**Crea:** `docs/adr/` directory con:
- `ADR-001-wpf-ui-library.md` — perché WPF-UI invece di MahApps/HandyControl
- `ADR-002-agpl-license.md` — scelta licenza AGPL vs MIT/GPL
- `ADR-003-wmi-event-monitoring.md` — WMI vs ETW per process events
- `ADR-004-no-codesigning.md` — distribuzione senza certificato, impatto su SmartScreen, mitigazioni

### 6.9 Troubleshooting / FAQ
**Crea:** `docs/TROUBLESHOOTING.md`
Sezioni: SmartScreen warning (no cert), richiesta UAC, WMI errors, performance counter access denied, app non parte su Windows 10.

### 6.10 Social proof nel README
**File:** `README.md`
- Aggiungi badges: build status, release version, license, codecov, winget version.
- Aggiungi screenshot dell'app (sezione "Screenshots").
- Aggiungi sezione "Comparison with Process Lasso" con tabella feature.

---

## SEZIONE 7 — FUNZIONALITÀ / UX

### 7.1 Smoke test flag CLI
**File:** `App.xaml.cs`
- Implementa gestione argomenti: se `--smoke-test` è presente, inizializza DI container, verifica che tutti i servizi si resolvano, log "OK", `Application.Current.Shutdown(0)`.

### 7.2 Re-abilitare LogViewerView
**File:** `ThreadPilot.csproj` + `MainWindow.xaml`
- Rimuovi le exclusion di `LogViewerView.xaml` e `LogViewerView.xaml.cs` dal csproj.
- Aggiungi tab "Log" nella navigation di MainWindow.
- Il viewer è già implementato (`LogViewerViewModel` esiste), basta esporlo.

### 7.3 Versione visibile nell'UI
**File:** `Views/SettingsView.xaml` o footer di MainWindow
- Mostra `Assembly.GetExecutingAssembly().GetName().Version` o `FileVersionInfo` nell'UI (es. footer "ThreadPilot v1.1.1").

---

## Ordine di implementazione consigliato

| Priorità | Sezione | Motivo |
|---|---|---|
| 1 | §1 Bug critici | Stabilità, resource leak |
| 2 | §2 Performance | GC pressure, hang WMI |
| 3 | §4 Code quality | Manutenibilità |
| 4 | §3 Sicurezza | Protected process denylist |
| 5 | §5 Distribuzione winget/choco | Distribuzione pubblica |
| 6 | §6 Repository professionale | Immagine progetto |
| 7 | §7 Funzionalità | UX polish |

---

## File da creare (nuovi)
- `global.json`
- `cliff.toml`
- `winget/manifests/p/PrimeBuild/ThreadPilot/1.1.1/*.yaml` (3 file)
- `chocolatey/threadpilot.nuspec`
- `chocolatey/tools/chocolateyInstall.ps1`
- `chocolatey/tools/chocolateyUninstall.ps1`
- `chocolatey/tools/LICENSE.txt`
- `docs/adr/ADR-001-wpf-ui-library.md`
- `docs/adr/ADR-002-agpl-license.md`
- `docs/adr/ADR-003-wmi-event-monitoring.md`
- `docs/adr/ADR-004-no-codesigning.md`
- `docs/TROUBLESHOOTING.md`
- `CONTRIBUTORS.md`
- `Helpers/DwmHelper.cs`
- `Services/ProcessFilterService.cs`

## File da modificare (esistenti)
- `Services/ProcessMonitorService.cs` — §1.1, §1.2, §2.1, §4.4
- `Services/PerformanceMonitoringService.cs` — §1.3, §2.2, §2.3, §2.4, §4.5
- `Services/ProcessService.cs` — §1.4, §2.3
- `Services/SecurityService.cs` — §3.1, §3.2
- `Models/LogEventTypes.cs` — §2.5
- `ViewModels/ProcessViewModel.cs` — §4.2
- `MainWindow.xaml.cs` — §4.1
- `README.md` — §5.3, §6.10
- `CONTRIBUTING.md` — §6.6
- `.github/workflows/release.yml` — §5.1 (hash step), §6.2, §6.4, §6.5
- `.github/workflows/ci-devsecops.yml` — §6.3
- `ThreadPilot.csproj` — §7.2
- `Tests/ThreadPilot.Core.Tests/ThreadPilot.Core.Tests.csproj` — §6.3
- `App.xaml.cs` — §7.1
