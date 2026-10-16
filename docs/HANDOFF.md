# Handoff ThreadPilot — 2026-10-16

## Versione e ambito

La versione preparata è **v1.7.7-beta.2**, esclusivamente pre-release. Include la pila #67–#70, le correzioni della topologia CPU e Game Boost (#71), comprese le modifiche locali del 15 ottobre alla diagnostica Attività. La conferma hardware EcoQoS di SA-Mousavichashmi è nella #66; non equivale alla validazione di Game Boost.

Fonte pubblica per funzionalità, limiti, test richiesti e dati da allegare alle issue: `docs/releases/v1.7.7-beta.2.md`. Queste note vengono usate dal workflow Release. v1.7.6 deve restare stable/latest; la beta non va pubblicata su winget o Chocolatey. I binari sono ancora unsigned.

## Validazione hardware ancora necessaria

- Game Boost su Ryzen dual-CCD X3D (7900X3D/7950X3D/9900X3D/9950X3D), incluso il parcheggio dei core del driver AMD.
- AMD a CCD uguali (7950X/9950X): confronto Automatico/Isolamento.
- Intel ibrida: P-core al gioco, E-core al background sulle topologie supportate; ripiego conservativo se il lato gioco ha meno di sei core fisici.
- Benchmark ripetibili con/senza background e Above Normal, almeno tre passaggi per scenario; FPS medi, 1% low, P95/P99 dei frame time.
- Giochi protetti/anti-cheat reali, stuttering, crash e regressioni.

QA precedente: Ryzen 7 5800X3D single-CCD e VM Windows 11 pulita; impostazioni, sessioni, priorità, rispetto delle modifiche manuali/regole e recupero dopo crash. Il test nativo CPU Sets usa vere chiamate Windows ma topologia dual-L3 simulata: non dimostra il comportamento su vero hardware multi-CCD. I test nativi Game Boost non esercitano lo split sui piccoli runner CI.

## Verifiche e pubblicazione

- Test Release locali dopo la preparazione beta: 851 passati; audit dipendenze senza vulnerabilità segnalate; installer/portable generati e smoke test del single-file riuscito.
- Causa della CI EcoQoS: Server 2022 build 20348 rifiuta GetProcessInformation(ProcessPowerThrottling) con errore 87 sia con query limitata sia completa. I job con test nativi e smoke usano ora Server 2025, coerente con il target Windows 11; il round-trip reale non viene saltato.
- Per stato effettivo di merge/release/CI consultare GitHub, non dedurlo dalla presenza di questo file: `gh pr list`, `gh run list`, `gh release view v1.7.7-beta.2`.
- Prima del tag verificare CI della combinazione finale e corrispondenza del codice con tutte le modifiche locali. Il tag deve puntare al commit integrato in main.
- Dopo il tag verificare artifact installer/portable/SHA256, smoke test, flag prerelease=true e `/releases/latest` ancora su v1.7.6.
- Pulire solo i branch della pila e Game Boost dopo l'integrazione; lasciare intatti worktree e branch di altri lavori.

## Convenzioni

- Fine riga misti: prima del commit confrontare `git diff --numstat` e `git diff --ignore-cr-at-eol --numstat`.
- Sette lingue in `Locales/*.xaml`, con le stesse chiavi; layout coperto dai test di contratto XAML.
- Per altri dettagli di sviluppo: `docs/IMPLEMENTATION_PLAN.md`, `docs/UX_REDESIGN_PLAN.md`, `docs/release/RELEASE_RUNBOOK.md`.
