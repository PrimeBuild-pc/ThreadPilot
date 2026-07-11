## ThreadPilot v1.4.3

Patch release expanding and validating ThreadPilot's bundled Windows power-plan catalog.

### Added

- Added 30 new bundled Windows power plans: 0 Synez Public Power, arsenha low latency, arsenha low latency (Intel Thread Director fix), AutoOS, BEYOND PERFORMANCE AMD+INTEL, Bitsum Highest Performance, cactusOS, FPSHEAVEN2026, GALA's ultimate performance (AMD), Gavot Performance, GTweaks Power Plan V3, imribiy2026, IrisFixed, JokrOS Power Plan, Jackpot2026, Kizzimo's Extreme Low Latency, KSOS11, melody LowestLatency, Microsoft High performance, Microsoft Ultimate Performance, Mitstas IDLE ENABLED, n1kobg GPU Booster Power Plan, Prodazin Power Plan, Reticle v2, RevisionPowerPlanV2.8, RIP Tweaks Power Plan, Rosca Tweaks v2, Velo's Power Plan, VTRL Optimized, and XNRL Pro Plan.
- Added structural, discovery, duplicate, packaging, and invalid-file tests for bundled power plans.

### Changed

- Updated four existing bundled plans: IIIEXOIII LOW LATENCY, LLG parking/E-core fix, Slower, and xilly.
- Removed a redundant duplicate Sazinho power-plan file.
- Power-plan display-name parsing now preserves names containing parentheses.

### Validation

- Every changed `.pow` file was imported successfully with `powercfg` using a temporary GUID and then removed; the active Windows power plan remained unchanged.
- Bundled assets are discovered automatically and copied to build, publish, portable ZIP, and installer output through the existing project and release workflow.
