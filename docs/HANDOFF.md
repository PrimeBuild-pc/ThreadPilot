# Handoff ThreadPilot — 2026-10-16

## Due canali richiesti

- **v1.7.7 stable:** EcoQoS, affidabilità regole/backup e redesign UI. Nessun Game Boost né correzioni aggiuntive della topologia introdotte con quel lavoro. Note: `docs/releases/v1.7.7.md`.
- **v1.7.8-beta.1:** tutto il codice locale Game Boost, comprese le modifiche Attività del 15 ottobre, più le correzioni Intel P/E e gruppi L3/CCD AMD. Deve restare pre-release, senza sostituire la stable/latest o pubblicare su winget/Chocolatey.

La versione combinata preparata prima della separazione è conservata nel commit `ef2ec24`. Non era stata pubblicata come v1.7.7-beta.2. La #67 consegna la stable senza Game Boost; la beta ripristina esattamente il codice runtime Game Boost di `6c36ea6` sopra la stable, con metadati separati. Note pubbliche e istruzioni tester: `docs/releases/v1.7.8-beta.1.md`. Il proprietario ha autorizzato il merge amministratore come PrimeBuild-pc dopo CI/CodeQL verdi.

## CI e validazione

Server 2022 build 20348 rifiuta GetProcessInformation(ProcessPowerThrottling) con errore 87 sia con query limitata sia completa. I job con test nativi e smoke usano Server 2025, coerente con il target Windows 11. Il round-trip EcoQoS reale resta attivo e passa. La conferma hardware EcoQoS di SA-Mousavichashmi nella #66 non equivale alla validazione Game Boost.

Il codice combinato Game Boost ha superato 851 test Release, CI DevSecOps, CodeQL, audit dipendenze/Gitleaks; installer/portable locali e smoke test single-file riusciti. Eseguire i controlli anche sui due commit finali separati.

## Game Boost: cosa manca

- Hardware reale Ryzen dual-CCD X3D, AMD a CCD uguali e Intel ibride supportate.
- Interazioni con parcheggio core/driver AMD; giochi protetti e anti-cheat reali.
- Benchmark comparabili ripetuti: FPS medi, 1% low, P95/P99 frame time e varianza, con/senza background e Above Normal.

QA precedente: Ryzen 7 5800X3D single-CCD e VM Windows 11 pulita per impostazioni/sessioni/priorità/ripristino. Il test nativo CPU Sets usa chiamate Windows vere ma topologia dual-L3 simulata. Il comportamento sul vero hardware multi-CCD resta da verificare. Ripiego conservativo se il lato gioco ha meno di sei core fisici.

## Verifica pubblicazione

Consultare GitHub per lo stato effettivo: `gh pr list`, `gh run list`, `gh release view v1.7.7`, `gh release view v1.7.8-beta.1`.

I tag devono puntare ai rispettivi commit integrati e testati. Verificare installer/portable/SHA256 e smoke test di ogni canale. La latest deve restare v1.7.7 dopo la pubblicazione della beta. I binari sono ancora unsigned. Pulire solo i branch della pila e Game Boost a integrazione conclusa, senza toccare altri worktree/lavori.

Fine riga misti: confrontare `git diff --numstat` con `git diff --ignore-cr-at-eol --numstat`. Sette lingue in `Locales/*.xaml`, stesse chiavi e contratti XAML nei test. Altri riferimenti: `docs/IMPLEMENTATION_PLAN.md`, `docs/UX_REDESIGN_PLAN.md`, `docs/release/RELEASE_RUNBOOK.md`.
