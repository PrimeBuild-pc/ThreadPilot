## ThreadPilot v1.6.0

ThreadPilot 1.6.0 adds explicit CPU assignment strategies while keeping the established ThreadPilot automatic behavior as the persistent default.

### Highlights

- Choose between ThreadPilot automatic, Affinity Mask, Ideal Processor, and CPU Sets.
- Save a global default or override the strategy for individual process rules.
- Apply topology-aware affinity across processor groups and processor indexes above 63.
- Distribute Ideal Processor preferences deterministically across existing process threads.
- Inspect reserved CPU Sets without changing global Windows configuration.
- Use the new controls and explanations in all seven supported languages.

### Compatibility and safety

- Existing settings and rules remain compatible and continue in ThreadPilot automatic mode.
- Explicit CPU Sets and Ideal Processor modes do not silently fall back to affinity.
- ThreadPilot never widens an existing hard affinity restriction; switching to a soft mode can require restarting the target process.
- With automation monitoring disabled, Ideal Processor and multi-group Affinity apply only to threads that exist at apply time.
- Reserved CPU Sets remain read-only in this release.

### Validation

- 689 automated tests passed in Debug and Release configurations.
- All four strategies passed hardware smoke tests on a Windows 11 Hyper-V guest with four logical processors.
- Multi-group and processor indexes above 63 passed synthetic topology tests.
