# Miglioramenti ThreadPilot — 12 ottobre 2026

Base: main `70b7f6d` (PR #65). Piano approvato da Lorenzo; implementazione locale sul branch `codex/ux-rules-reliability`.

## Ordine di esecuzione

- [ ] 1. Backup completo: esportare anche le regole persistenti, mantenere compatibilità con i backup precedenti, validare prima di applicare, mostrare il riepilogo delle sostituzioni e ripristinare in caso di errore.
- [ ] 2. Regole coerenti: precedenza deterministica, elenco delle corrispondenze e avviso dei conflitti; usare la stessa selezione nel motore e nel riepilogo.
- [ ] 3. Processi: elenco con colonne opzionali, pannello dettagli adattivo, priorità CPU/memoria/I/O visibili, comandi applica/salva chiari, sezione avanzata chiusa inizialmente, contatore comprensibile.
- [ ] 4. Editor: distinguere automazioni energetiche e impostazioni dei processi; preparare tutte le modifiche e salvarle insieme; segnalare modifiche pendenti.
- [ ] 5. Pausa automazioni: mantenere disponibile il controllo manuale e mostrare un avviso persistente.
- [ ] 6. Accessibilità e lingua: testi operativi leggibili, descrizioni dinamiche localizzate, focus/tastiera, etichette e layout ridimensionabile.
- [ ] 7. Ripristino: annullare le modifiche manuali di priorità sullo stesso processo; per i tweak indicare ambito, riavvio e comportamento di ripristino, usando i backup esistenti.
- [ ] 8. Reattività: misurare startup/consumi, eliminare le attese sincrone evitabili nell'avvio conservando elevazione, istanza unica e gestione errori; mantenere la sospensione degli aggiornamenti nascosti.
- [ ] 9. Verifica finale: test mirati, suite completa, build Debug/Release, formattazione, controllo XAML/localizzazioni e misure ripetibili; documentare limiti del collaudo grafico.

## Ambito e cautele

Servizi di persistenza/regole, view model e viste Processi/Regole/Impostazioni/Tweak, startup, dizionari delle sette lingue e relativi test. Nessuna nuova dipendenza o migrazione del framework. Importazioni e rollback devono preservare i dati; i comandi di ripristino non devono agire su PID riutilizzati. Le precedenze nuove devono essere visibili e testate.

## Pulizia e verifica

Sostituito `IMPROVE_APP_PERFORMANCE_PLAN.md`, relativo alle release 1.4.3 e PR #35/#36 ormai superate. Le ottimizzazioni già presenti (cache regole, caricamento pagine differito, aggiornamenti sospesi in tray) vengono conservate. Vecchia misura storica: tray 0,0000% CPU nel campione, 140,24 MB working set, zero processi figli; non è una baseline dell'attuale macchina/versione. Documenti di release e licenze restano documentazione storica, non piani da cancellare.

## Risultati

Da aggiornare durante l'implementazione.
