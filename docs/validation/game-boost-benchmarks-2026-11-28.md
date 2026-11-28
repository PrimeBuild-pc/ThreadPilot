# Game Boost benchmarks on the machine available on 28 November 2026

Live check on this PC only. Defaults were not changed.

| | |
|---|---|
| CPU | AMD Ryzen 7 5800X3D, 8 cores / 16 logical processors, one package, one L3 group |
| Power plan | `zoic's ultimate performance(amd)`, not Balanced |
| Arma Reforger | Not installed and not running |
| Decision | `GameBoostPolicy.Decide` on the live topology, brand `AMD Ryzen 7 5800X3D 8-Core Processor`: **Unsupported** in both Automatic and Isolation. No game CPU Set and no background CPU Set. |
| Thresholds left as they are | Release 0.8 / 4 samples, confine 0.5 / 20 samples, max 2 releases, minimum 6 physical cores |

A single-CCD CPU cannot show Game Boost confinement, dynamic release, or a frame-time difference. Those checks still need another PC.

## Still needs a suitable PC

- AMD dual-CCD (asymmetric 3D V-Cache, and symmetric parts such as 9950X) with a CPU-bound game, Balanced plan, and frame times.
- Intel hybrid other than the i9-12900K already recorded in `intel-hybrid-12900k.md`. That note did not measure CPU-bound gameplay or the Balanced plan.
- The dynamic release/confine loop, which only runs when the game is actually confined.
- The Processes list at 0.0% CPU for `ArmaReforgerSteam`. Not reproduced here. On this PC, `Process.TotalProcessorTime` throws for protected system processes, and `GetProcessTimes` with `PROCESS_QUERY_LIMITED_INFORMATION` fails for the same processes, so that fallback would not explain a process whose CPU time .NET can already read. Left in `ProcessService` for Codex if Arma is available.

## What this session did verify

- Read-only CPU Set diagnostic: no override, explicit override, access denied, partial mapping, more than one processor group, and a hard affinity that does not cover the assignment. A live thread on this PC was given one CPU Set, read back, and cleared.
- The core-mask picker no longer stays on All Cores when the displayed selection is a CPU Set subset.
- Isolation text for an Intel hybrid decision no longer cites CCD0. The CCD0 sentence remains only for two equal AMD CCDs.
