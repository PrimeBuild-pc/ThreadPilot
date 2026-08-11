## ThreadPilot v1.5.3

ThreadPilot 1.5.3 hardens persistence, monitoring, notifications, autostart, tray updates, and application shutdown.

### Highlights

- Saved process rules are protected from overwrite after transient read or recovery-copy failures.
- Pending Settings edits now merge safely with unrelated background updates.
- CPU monitoring preserves logical processor identity and avoids overlapping callbacks after rapid stop/start cycles.
- Notification quiet hours, retries, default hotkeys, autostart ordering, tray refreshes, and shutdown disposal are more reliable.
- Process Management now shows a clean disabled state when automation monitoring is off.

### Validation

- 675 automated Release tests passed before release preparation.
- CI DevSecOps and CodeQL passed on the merged reliability changes.
- The elevated Windows UI and disabled-monitoring state were manually validated.
