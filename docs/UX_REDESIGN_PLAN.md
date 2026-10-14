# Redesign UX — chiarezza (ottobre 2026)

Branch `feat/ux-clarity`, impilato su #69 (EcoQoS). Nessuna modifica al formato dei dati: regole, impostazioni e backup restano compatibili.

## Problemi osservati (screenshot dell'app 1.7.6)

- Menu laterale solo a icone: sette icone senza etichetta.
- Processi: casella di ricerca senza testo guida, nove controlli sparsi nella barra, pannello dettagli come un'unica colonna di etichette e paragrafi, selettori che non mostrano il valore attuale del processo.
- Regole: tre schede e la prima aperta è la meno usata (associazioni dei piani energetici); nell'editor le scelte compaiono come nomi interni (`BelowNormal`, `SystemManaged`).
- Sottotitoli lunghi su quasi tutte le pagine.

## Regole di stile

1. Titolo della pagina e al massimo una riga breve; le spiegazioni vanno nei tooltip.
2. Una sezione = un riquadro con un titolo breve.
3. Un solo pulsante principale (accent) per riquadro.
4. I selettori mostrano lo stato attuale; "Applica" agisce solo sul valore cambiato.
5. Etichette leggibili per le persone, mai nomi interni di enum.

## Interventi

- [x] Menu laterale aperto con etichette brevi (Processi, Maschere CPU, Piani energetici, Regole, Attività, Ottimizzazioni).
- [x] Processi: ricerca con testo guida; filtri e colonne in due menu a tendina; dettagli in tre riquadri (Processo, Priorità, Core CPU) più la riga della regola; selettori allineati allo stato attuale; testi di aiuto nei tooltip.
- [x] Regole: scheda "Regole dei processi" per prima, nomi delle schede più chiari, etichette localizzate nell'editor.
- [x] Sottotitoli accorciati in tutte le lingue.
- [ ] Impostazioni e Ottimizzazioni con righe in stile Impostazioni di Windows 11 (`CardControl`): prossimo passo, richiede una riscrittura più ampia delle due viste.
- [ ] Area unica per gli avvisi della finestra principale: da valutare dopo la verifica sul campo.

## Verifica

Test XAML esistenti aggiornati dove cambiano i contratti, suite completa, build Debug/Release, screenshot prima/dopo di ogni pagina.
