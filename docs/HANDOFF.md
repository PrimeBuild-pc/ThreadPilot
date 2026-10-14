# Handoff ThreadPilot — aggiornato 2026-10-14

Piani di riferimento: `docs/IMPLEMENTATION_PLAN.md` (affidabilità regole/UX) e `docs/UX_REDESIGN_PLAN.md` (redesign). Cancellare questo file quando non serve più.

## Stato

- **Quattro PR aperte, impilate e non mergiate.** Vanno unite in quest'ordine, ognuna sopra la precedente:
  1. #67 `fix/backup-rule-precedence` → `main`: il backup include le regole persistenti; ordine di applicazione delle regole deterministico, con avviso dei conflitti.
  2. #68 `codex/ux-rules-reliability` → #67: editor di processi e regole, annulla ultima priorità, automazione in pausa senza overlay.
  3. #69 `feat/ecoqos-efficiency-mode` → #68: modalità efficienza (EcoQoS) per i processi e nelle regole (issue #66), con tre stati: decide Windows / attiva / disattivata.
  4. #70 `feat/ux-clarity` → #69: redesign completo:
     - menu con etichette, pagina Processi a schede, Regole, Impostazioni e Ottimizzazioni in stile Windows 11, interruttori nativi;
     - layout ricordato in `ui-state.json`: barra laterale, finestra, filtri, colonne;
     - correzioni trovate in VM: contatore processi, barra stretta, scala 150%.
- **Pre-release pubblica [v1.7.7-beta.1](https://github.com/PrimeBuild-pc/ThreadPilot/releases/tag/v1.7.7-beta.1)**: commit `20abf70`, installer e SHA256. La "latest" resta v1.7.6, quindi l'aggiornamento automatico non la propone. Non contiene l'ultimo commit di #70 (`1544587`: persistenza layout e interruttori); per EcoQoS non cambia nulla.
- **In attesa**: risposta di chi ha aperto la #66 (SA-Mousavichashmi) con il test su CPU ibrida con E-core; gli è stato chiesto anche modello CPU e build di Windows.
- Test: 791 passano. Build Debug e Release pulite.

## Prossimi passi

1. **Se la #66 conferma che funziona:**
   - merge #67 → #68 → #69 → #70, in ordine; dopo ogni merge ribasare la PR successiva su `main`;
   - release 1.7.7: versione in `ThreadPilot.csproj` (oggi 1.7.6), CHANGELOG, `build/build-installer.ps1 -Version 1.7.7`.
2. **Se non funziona:** correggere su `feat/ecoqos-efficiency-mode` e propagare con un merge verso `feat/ux-clarity`.
3. Facoltativi:
   - salvare anche la scheda aperta in Regole e i filtri di Attività;
   - la lista processi ordinata per CPU si riordina sotto il cursore (problema che c'era già prima; oggi lo evita solo "Blocca l'elenco");
   - le opzioni dell'editor di regole e l'elenco regole sono localizzati, ma i messaggi di errore di `ProcessOperationUserMessages` sono ancora solo in inglese;
   - provare la persistenza della finestra con più monitor.

## Promemoria di lavoro

- **Fine riga misti nel repo:** le `.cs` sono quasi tutte CRLF, le XAML e `Locales/*.xaml` LF, alcuni file misti. `sed -i` di Git Bash e la riscrittura completa dei file cambiano i fine riga: prima di ogni commit confrontare `git diff --numstat` con `--ignore-cr-at-eol`, e per i file misti reinserire le modifiche sul contenuto di HEAD.
- **Traduzioni:** sette lingue in `Locales/*.xaml`, tutte con le stesse chiavi. Le chiavi mancanti nella XAML compaiono vuote nell'interfaccia.
- **Test UI:** le view hanno test di "contratto" XAML (`*XamlBindingTests`, `SettingsViewModelThemeTests`, `SystemTweaksStartupAndStyleTests`), da aggiornare quando cambia il layout. I test scrivono `ui-state.json` in una cartella temporanea (`TestUiStateIsolation`).
- **VM per test grafici:** `NeuroTune-W11`, procedura nella memoria `vm-gui-testing`. Fare sempre il checkpoint prima e il ripristino dopo; risoluzione 1080p e autologon solo durante il test.
- **Shell elevata:** si può avviare l'app (`requireAdministrator`) e fare screenshot con PrintWindow e clic dalle UIAutomation bounds.
- **Git e GitHub:** account `PrimeBuild-pc`. Niente attribuzioni a Claude in commit e PR.
