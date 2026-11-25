# Game Boost on a real Intel hybrid CPU (Core i9-12900K)

This is the record of one validation session of Game Boost on real Alder Lake hardware: what was measured,
what was found broken, what was fixed on branch `validation/intel-hybrid-12900k`, and what is still open.
Every result is tagged with how it was established:

- **HW** – verified on the real i9-12900K (numbers read from Windows, not from the ThreadPilot UI)
- **Test** – verified only by automated tests or a simulated topology
- **Inferred** – reasoned from the code or from other measurements, not measured
- **Not run** – not executed (reason given)

## Test system

| | |
|---|---|
| CPU | Intel Core i9-12900K: 8 P-cores with Hyper-Threading + 8 E-cores, 24 logical processors, one 30 MB L3 |
| RAM / GPU | 32 GB / NVIDIA GeForce RTX 4070 Ti (driver 32.0.16.2140) |
| Board / BIOS | ASUS TUF GAMING Z690-PLUS D4, BIOS 4701 |
| OS | Windows 11 Pro 26H2, build 26300.9974; VBS/HVCI not running |
| Power plan | Ultimate Performance ("Prestazioni eccellenti"): core parking minimum 100 %, heterogeneous thread scheduling policy 5 (automatic) |
| ThreadPilot | 1.7.11, starting commit `4fa24ee`, Italian UI |
| Tools | Throw-away tools kept outside the repository: a CPU Set reader (`GetSystemCpuSetInformation`, `GetProcessDefaultCpuSets`, `GetProcessInformation(ProcessPowerThrottling)`), a kernel context-switch tracer (TraceEvent, run time of every process on every CPU), a controllable fake game installed under `…\steamapps\common\FakeGame\`, renamed copies of it as `chrome`/`steam`/`discord`/`Spotify`, and PresentMon 2.6.0 for frame times |

CPU Set table as Windows reports it (HW): 24 CPU Sets, IDs 256–279; EfficiencyClass 1 on logical processors
0–15 (CoreIndex 0, 2, …, 14: SMT pairs (0,1)…(14,15)); EfficiencyClass 0 on logical processors 16–23 (one per
core); one L3 group. This exact table is now a regression test (`GameBoostIntelHybridTests`).

## Results V1–V12

| # | Check | Result | How |
|---|---|---|---|
| V1 | Topology and decision | 24 CPU Sets, P = class 1 (16 CPUs), E = class 0 (8 CPUs). The Optimizations page shows "game on the P-cores (16 CPUs), selected background apps on the E-cores (8 CPUs)" in both modes. | HW |
| V2 | Detection and session | Fake game in `\steamapps\common\` starts a session within one 3 s tick; Alt+Tab to ThreadPilot keeps it; closing the game ends it. Real games: Farming Simulator 25 and Call of Duty detected; **Arma Reforger was not detected before the fix** (see problem 2), detected after it. | HW |
| V3 | CPU Sets read from Windows | Automatic: game → CPU 0–15 + high QoS; listed background (`chrome`, `steam`, `steamwebhelper` ×6) → CPU 16–23; unlisted processes untouched. Isolation (switched mid-session): additionally `Spotify` moved, EcoQoS on the listed background, game keeps all 16 P threads. | HW |
| V4 | Restore | Game exit, switch off, mode change mid-session, `taskkill /f` mid-session (journal with 8 confirmed entries on disk, everything restored at the next start) all restore every change. A user rule (CPU Sets 0,2,4,6) on the game is left alone ("cores left to your rule"). A game's own `PROCESS_POWER_THROTTLING_IGNORE_TIMER_RESOLUTION` flag is kept (ControlMask 0x5 / StateMask 0x4 during the session, 0x4 / 0x4 after). | HW |
| V5 | Dynamic release | 20 busy threads on the 16 P CPUs: released after **13.3 s** and **15.8 s** (expected 12–15 s); load back to 4 threads: confined again after **63 s** (expected ~60 s); after the 2nd release the game stays free for the rest of the session. | HW |
| V6 | What the user sees | Processes page did show the CPU Sets for a boosted game, but in English, without saying who set them, next to a status line printing the full OS affinity. Fixed: localized line naming Game Boost / your rule / other, and a "no CPU Sets, Windows picks the cores" note. The real cause of "nothing shown" for Arma was V2 (nothing was applied). | HW (before and after) + Test |
| V7 | Windows Game Mode | Reproduced: with `AutoGameModeEnabled` absent the page said "Disabled" while Windows uses its default (enabled). Fixed; registry restored after the test. | HW (repro) + Test |
| V8 | Real games | Arma Reforger (offline training scene, ~120 fps GPU/limit-bound) and Farming Simulator 25 (main menu only). See below. | HW |
| V8-bis | Call of Duty | Detected; **CPU Sets and QoS refused**: Ricochet returns ERROR_ACCESS_DENIED (5) from `SetProcessDefaultCpuSets` for an unsigned caller although `OpenProcess` succeeds. Not worked around. Assetto Corsa Competizione / Rally could not be started without interacting with a Steam dialog: not run. | HW |
| V9 | Diagnostics | Activity entries (session start/end, mode, what was applied or skipped and why, dynamic release/confine, restore) now also go to `ThreadPilot.log`; new one-line reports for a foreground process whose path cannot be read and for a failing tick. | HW + Test |
| V10 | Windows / Thread Director | Plan parks no cores and uses automatic heterogeneous scheduling: no conflict with CPU Sets, no warning needed. Without Game Boost Windows did **not** keep light background work on the E-cores (fake background at 30 % duty ran ~88 % on P-cores), and let Arma's workers spread evenly onto the E-cores. Balanced plan not tried (not changing system settings). | HW (partial) |
| V11 | 12th/13th/14th gen, Core Ultra | Real 12900K table + synthetic 13900K/14900K/14700K/13700K/12600K/13600K, 12400 (no E-cores: no action), Core Ultra with three classes, Arrow Lake without SMT, Lunar Lake (4 P-cores: no action). | Test |
| V12 | Cost and leftovers | Idle, no game, main window open: ThreadPilot used 1.37 % of one CPU over 120 s with Game Boost on and 1.21 % with it off (0.06 % / 0.05 % of the machine), ~215 MB working set. No warning or error in the log apart from the expected Call of Duty refusals. No process left with CPU Sets after all tests except one `svchost` whose all-24 CPU Sets were already there before ThreadPilot ran. | HW |

### V8 numbers (45 s windows, kernel context-switch accounting)

| Scene | Game Boost | Game on P (CPUs) | Game on E (CPUs) | P busy avg | E busy avg | avg fps | 1 % low fps |
|---|---|---|---|---|---|---|---|
| Arma Reforger training | off | 2.69 | **1.66** | 17.9 % | **22.3 %** | 121.7 | 99.3 |
| Arma Reforger training | Automatic | 4.21 | 0 | 27.5 % | 2.6 % | 121.6 | 101.0 |
| Arma Reforger training | Isolation | 4.14 | 0 | 27.1 % | 2.2 % | 122.0 | 102.8 |
| Arma Reforger training (2nd run) | Automatic | 4.44 | 0 | 28.7 % | 2.1 % | 119.9 | 97.8 |
| Arma Reforger training (2nd run) | off, CPU Sets = one thread per P-core (CPU 0,2,…,14) | 3.15 | 0 | 20.6 % | 1.7 % | 120.9 | 87.5 |
| Arma Reforger training (2nd run) | off | 2.71 | 1.66 | 18.0 % | 22.2 % | 122.8 | 101.0 |
| Farming Simulator 25 main menu | off / Automatic / Isolation | 0.27 | 0 | ~2 % | <1 % | 60.2 | ~58.7 |
| Call of Duty menu, `RendererWorkerCount = 7`, first start after a driver update (shader compilation) | on (refused for the game) | 6.21 | 2.51 | 44.5 % | 34.9 % | 30.2 | 26.3 |
| Call of Duty menu, default `RendererWorkerCount` | on (refused for the game) | 1.17 | 0 | 13.8 % | 1.1 % | 30.0 | 29.0 |

What this shows, and what it does not:

- Game Boost does what it says on this CPU: a detected, writable game runs only on the P-core threads and
  the listed background only on the E-cores.
- **No frame-rate benefit was measured.** Arma Reforger's training scene is GPU/limit-bound at ~120 fps and every
  configuration gives the same average; 1 % lows differ by a few fps between single 45 s runs, within run-to-run
  noise. The Farming Simulator 25 and Call of Duty menus are frame-capped (60 / 30 fps). Saved games were not
  loaded, to leave the owner's saves untouched, so CPU-bound gameplay was not measured.
- The "one thread per P-core" mask did not help Arma; its single lower 1 % low is not enough to call it worse.
- `RendererWorkerCount = 7` does not keep Call of Duty off the SMT siblings or the E-cores in the menu: during
  shader compilation `cod.exe` ran on all 24 logical processors (2.5 CPUs on the E-cores). The setting limits the
  render workers only. The two CoD rows are not comparable to each other (the first one includes shader
  compilation after a driver update).

## The three reported problems

### 1. All eight E-cores evenly at ~25 % during Arma Reforger / Farming Simulator 25

Measured cause (HW): with Game Boost not applied to the game, **Arma Reforger's own worker threads** run on the
E-cores: 1.66 CPUs of `ArmaReforgerSteam` spread evenly, ~21 % on each of the eight E-cores, and nothing else
of significance there (BattlEye's `BEService` < 0.05 CPU, Steam < 0.01). With Game Boost applied the E-cores
drop to ~2–3 % and the game's work moves to the P-core threads. In the reported session Game Boost was not
being applied to Arma at all (problem 2), which matches the picture. The even ~0 % on the second SMT threads
is Windows preferring one thread per P-core before the siblings. Farming Simulator 25 could only be measured
in its menu (0.27 CPU, all on P-cores with or without Game Boost), so its in-game E-core load is not explained
by a measurement here.

### 2. "Processes" showed no Game Boost CPU Sets for the game

Two separate things:

1. **Arma Reforger was never boosted.** ThreadPilot read executable paths through `Process.MainModule`, which
   needs `PROCESS_VM_READ` to walk the module list in the target's memory. BattlEye refuses that right, so
   `ArmaReforgerSteam.exe` had an empty path, failed the `\steamapps\common\` check and no session started —
   silently. The same process accepts CPU Sets and QoS changes from ThreadPilot (verified by adding it to the
   manual list). Fixed by reading the path with `QueryFullProcessImageName` and only
   `PROCESS_QUERY_LIMITED_INFORMATION` (the right Task Manager uses). After the fix Arma is detected and confined
   with no manual entry (HW).
2. **The page did show CPU Sets for a boosted game** (verified with the fake game on the unmodified build), but
   as an English "Current CPU Sets: …" line without a source, next to "Affinity: 0xFFFFFF", which reads as
   "nothing applied". The line is now localized and says "set by Game Boost while the game runs", "from your
   saved rule" or "set by hand, by the app itself or by another tool"; it also wraps instead of being cut.

### 3. Windows Game Mode shown as disabled

`GameModeService` treated a missing `HKCU\Software\Microsoft\GameBar` key or `AutoGameModeEnabled` value as
"disabled". Windows writes the value only after the user flips the switch and defaults to enabled. Now only an
explicit 0 means off; unreadable values report the Windows default. `SetGameModeAsync` still writes an explicit
DWORD, so the switch and the page stay consistent.

### 4. Game Boost sessions missing from the log file

`ServiceConfiguration` calls `AddLogging` without any provider, so everything logged through `ILogger` —
including Game Boost's session messages — was discarded; `ThreadPilot.log` only receives
`IEnhancedLoggingService` events. Activity entries are now also written to the log file (one line per entry),
which covers Game Boost start/end, mode, applied/skipped with reasons, dynamic release/confine and restore.

## Commits on this branch

| Commit | Change | Tests |
|---|---|---|
| Treat a missing Game Mode registry value as enabled | V7 | `GameModeServiceTests` (scratch HKCU key) |
| Write Activity entries to the log file | V9 | `ActivityAuditServiceTests` |
| Say who set the CPU Sets shown on the Processes page | V6, 7 languages | `ProcessViewModelAffinityTests` |
| Report Game Boost skips that left no trace | V9 | `GameSessionServiceTests` |
| Add Intel hybrid regression tests from a real i9-12900K | V11 | `GameBoostIntelHybridTests` |
| Read process paths without PROCESS_VM_READ | V2 / problem 2 | `ProcessImagePathTests` (native) |
| Wrap the current CPU assignment line on the Processes page | V6 | – |

Baseline: 969 tests passed, 0 skipped (the native `GameSessionNativeIntegrationTests` run on this 24-thread CPU).
Branch: all tests pass (1001 at the time of writing); Debug and Release build with no new warnings.

## 12th, 13th and 14th generation and Core Ultra

- **Proven on hardware:** Alder Lake i9-12900K only (8P+8E, one L3, Windows 11 26H2).
- **Inferred for Raptor Lake / Raptor Lake Refresh (13th/14th gen):** same EfficiencyClass scheme (1 = P, 0 = E),
  P-cores with Hyper-Threading listed first, one L3. The policy picks the P threads for the game and the E-cores
  for the background exactly as on the 12900K (synthetic tests). With 16 E-cores (13900K/14900K) the background
  side is larger, which does not change anything for the game.
- **Inferred for Core Ultra:** only the highest EfficiencyClass is treated as P; every lower class, including
  low-power E-cores on a three-class part, goes to the background set. That is acceptable for background work,
  but on laptops the LP E-cores are slow, and the policy does not prefer the normal E-cores for the background.
  Parts with fewer than 6 P-cores (Lunar Lake, 2P+8E mobile chips) get no action. Arrow Lake desktop (8 P-cores
  without SMT) gives the game 8 CPUs. None of this was run on such hardware.
- **Anti-cheat:** Ricochet (Call of Duty) refuses CPU Set and QoS changes from ThreadPilot; BattlEye (Arma
  Reforger) accepts them. Other anti-cheats were not tested.

## Proposals (not implemented)

- **Dynamic release thresholds:** the measured timings match the design. On a 12900K "80 % of the game's CPUs"
  means ≥ 12.8 busy logical processors out of 16 (8 cores with SMT), so a game using one thread per P-core (8
  busy threads) is never released, which is the intended behaviour. Call of Duty's shader compilation (~8.7
  CPUs busy) would not trigger a release either. No change proposed without frame-time data from CPU-bound games.
- **"One thread per P-core" option:** feasible as a CPU Set selection (`CoreIndex`-unique logical processors of
  the top class). Not implemented: the only writable game tested did not benefit, and the game it would matter
  for (Call of Duty) cannot be changed by ThreadPilot anyway.
- **Code signing:** Ricochet's decision appears to depend on the caller (a Microsoft-signed process got the same
  calls accepted during diagnosis, an unsigned .NET executable got ERROR_ACCESS_DENIED). Whether a signed
  ThreadPilot would be accepted is unknown and was not tried; it is not a workaround to pursue against the
  anti-cheat's intent.
- **Isolation description:** the mode text mentions "CCD0" on Intel too; an Intel-specific sentence would be clearer.

## Still open

- CPU-bound gameplay (loaded saves) was not measured; no FPS claim is made either way.
- The Processes list showed 0.0 % CPU for `ArmaReforgerSteam` although .NET reads its CPU time; not investigated.
- The core mask picker keeps showing "All Cores" while a process has CPU Sets.
- Game Boost Activity messages are English (like the rest of the Activity log); only UI strings are localized.
- Balanced power plan comparison and Windows' own automatic EcoQoS decisions were not measured in depth.
