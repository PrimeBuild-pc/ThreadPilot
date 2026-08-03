## ThreadPilot v1.5.2

ThreadPilot 1.5.2 consolidates process-monitoring configuration so every monitoring control has a real runtime effect.

### Highlights

- Application settings are now the single runtime source for WMI monitoring, fallback polling, and fallback polling interval.
- Removed ineffective duplicate monitoring controls from Rules & Automation.
- Existing configuration JSON files remain compatible; obsolete duplicate fields are safely ignored when loaded.

### Validation

- 641 automated Release tests passed before release preparation.
- Release build completed successfully.
