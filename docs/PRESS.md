# ThreadPilot press kit

Everything you need to write about ThreadPilot. All images are real screenshots and may be used freely in coverage.

## In one line

ThreadPilot is a free, open-source Windows 11 app that does what paid process-control tools charge for: CPU Sets, per-app rules and power-plan automation, with an activity log and no telemetry.

## Short description (≈60 words)

ThreadPilot gives Windows 11 users precise control over how apps use the CPU. It pins processes to chosen cores with CPU Sets, saves per-app rules for priority, memory priority and EcoQoS, and switches power plans when a game starts. It understands Intel hybrid and multi-CCD AMD topologies, logs every change, has no telemetry and costs nothing, with no feature limits.

## Fact sheet

| | |
|---|---|
| Developer | Lorenzo, PrimeBuild (Italy) — builds low-cost gaming PCs and free Windows tools |
| Price | Free, no feature limits, no nag screens |
| License | AGPL-3.0, source on GitHub |
| Platform | Windows 11 (build 22000+), x64 |
| Latest version | see [Releases](https://github.com/PrimeBuild-pc/ThreadPilot/releases/latest) |
| Install | `winget install PrimeBuild.ThreadPilot` · `choco install threadpilot` · GitHub Releases |
| Downloads | 11,000+ from GitHub Releases (Nov 2026), plus WinGet and Chocolatey |
| Languages | 7 interface languages |
| Privacy | No telemetry ([PRIVACY.md](PRIVACY.md)) |

## What it does

- CPU Sets and affinity with topology-aware core selection: processor groups, systems with more than 64 logical processors, Intel hybrid detection from Windows' own `EfficiencyClass`.
- Saved per-process rules (CPU set, priority, memory priority, EcoQoS) applied automatically when the process starts.
- Power-plan switching when chosen apps start or stop.
- Guardrails: warning on High priority, Realtime blocked.
- Activity log of every change, with filters and export.

## Honest limits

- No automatic responsiveness tuning like Process Lasso's ProBalance; it applies the rules you set.
- Rules apply only while ThreadPilot runs (no Windows service).
- Windows 11 only; requires administrator rights.
- Releases are not code-signed yet, so SmartScreen may warn.
- Game Boost (automatic game/background CPU Sets) is off by default and not yet validated on dual-CCD Ryzen X3D and Intel hybrid CPUs.

## Images

| | |
|---|---|
| Demo GIF | [threadpilot-demo.gif](https://raw.githubusercontent.com/PrimeBuild-pc/ThreadPilot/main/docs/media/threadpilot-demo.gif) |
| Overview, dark and light | [overview-themes.png](https://raw.githubusercontent.com/PrimeBuild-pc/ThreadPilot/main/assets/screenshots/overview-themes.png) |
| Processes | [dark](https://raw.githubusercontent.com/PrimeBuild-pc/ThreadPilot/main/assets/screenshots/processes-dark.png) · [light](https://raw.githubusercontent.com/PrimeBuild-pc/ThreadPilot/main/assets/screenshots/processes-light.png) |
| CPU masks | [cpu-masks-light.png](https://raw.githubusercontent.com/PrimeBuild-pc/ThreadPilot/main/assets/screenshots/cpu-masks-light.png) |
| Rules | [rules-dark.png](https://raw.githubusercontent.com/PrimeBuild-pc/ThreadPilot/main/assets/screenshots/rules-dark.png) |
| Activity log | [activity-light.png](https://raw.githubusercontent.com/PrimeBuild-pc/ThreadPilot/main/assets/screenshots/activity-light.png) |
| Power plans | [power-plans-light.png](https://raw.githubusercontent.com/PrimeBuild-pc/ThreadPilot/main/assets/screenshots/power-plans-light.png) |
| App icon | [ico.ico](https://raw.githubusercontent.com/PrimeBuild-pc/ThreadPilot/main/assets/icons/ico.ico) |

## Contact

primebuild.official@gmail.com · [GitHub](https://github.com/PrimeBuild-pc/ThreadPilot) · [Discord](https://discord.gg/ERUwSxE79q) · [Instagram](https://www.instagram.com/prime_build_/)

Happy to answer questions, provide a quote, or walk through how the CPU Sets handling works.
