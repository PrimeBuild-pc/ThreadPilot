# Performance audit on an i9-12900K (background cost, startup, Game Boost, sim racing)

Measured on the same machine as `intel-hybrid-12900k.md` (i9-12900K, Windows 11 26H2, 153 processes running),
ThreadPilot 1.7.11 plus the commits on this branch. Tools: `dotnet-counters`, `dotnet-trace` (sampled thread time,
filtered to samples that are not parked in a wait), kernel CPU sampling with stacks (1 ms), process CPU time over
fixed windows. Single runs unless stated; differences under ~0.1 % of one core are within noise.

## Background CPU

| State | ThreadPilot CPU (one core = 100 %) |
|---|---|
| Started minimized to tray (autostart case), Game Boost off | 0.03–0.09 % |
| Started minimized, Game Boost on (3 s tick, no game) | 0.065 % (no measurable difference from off) |
| Game Boost session on Assetto Corsa Competizione, ThreadPilot in tray | 0.08 % |
| Main window open on the Processes page (5 s refresh) | 2.3 % |
| Window opened, then sent to the tray | 0.6–1.0 % (see below) |

- **Process monitoring is already event-driven.** Process start/stop come from `Win32_ProcessStartTrace` /
  `Win32_ProcessStopTrace` (extrinsic WMI events backed by kernel ETW, no `WITHIN` polling inside WMI). Polling only
  runs as a fallback when WMI is degraded. Moving to raw ETW would not lower the idle cost measurably: it is already
  below 0.1 % of one core.
- **Game Boost's 3 s tick costs nothing measurable** (it remembers a rejected foreground process for 30 s and otherwise
  only reads the foreground process id and start time). A foreground-change hook would only shorten the time to start a
  session from ≤ 3 s to ~0, which does not matter for games that take tens of seconds to load. Not changed.
- **Window open:** in the 2.3 % the managed work is WPF layout of the process list (rows re-measured because the
  list is sorted by CPU use and re-orders every refresh) and `Process.MainWindowHandle`, which ran a full
  `EnumWindows` for each of the ~150 processes. The latter is now one `EnumWindows` pass per refresh
  (≈2 ms → 0.02 ms per refresh). The DataGrid already uses container recycling; the remaining layout cost comes with
  a live sorted list.
- **Window opened, then hidden:** the refresh timers do stop (process list, tray status, power plans, performance
  page) and almost no managed code runs (5 non-waiting samples in 60 s), but the UI thread keeps doing native
  message-loop work (user32/win32k) and the WPF render thread wakes up. Mica backdrop accounts for part of it
  (A/B, one run each: 0.91 % with Mica, 0.57 % with no backdrop). Root cause of the rest not found; Mica not removed
  (design choice, small effect).

## Memory

| | Working set | Private bytes | GC heap |
|---|---|---|---|
| Single-file install, started minimized | ~182 MB | ~146 MB | ~28 MB |
| Folder (non-single-file) build, started minimized | ~180 MB | ~92 MB | – |
| Single-file install after the window was opened, hours later | ~400 MB | ~364 MB | ~28 MB |

The managed heap is small; most private memory is the runtime/WPF image data of the self-contained single-file
publish, and the visual tree once the window has been shown. Publishing as a folder saves ~50 MB private at start;
that is an installer/packaging decision and was not changed here.

## Startup

From process start: main window initialized after **0.83 s**, process monitoring active after **1.0 s** (log
timestamps; ~1.3 s of CPU). No change proposed. Note for future measurements: `ThreadPilot.log` is flushed every
5 s, so the file's modification time is not a startup marker.

## Assetto Corsa Competizione (no anti-cheat)

- Existing rule (P-core CPU Sets + Above Normal) applied **2.2 s** after process start; Game Boost logged
  "cores left to your rule", added high QoS and moved Steam, Steam web helpers and Edge to the E-cores.
- Priority stayed Above Normal for the full 120 s observation on the second launch. On the **first** launch it was
  found back at Normal about a minute later although ThreadPilot had verified it at +1 s and +4 s and logged no
  other priority write; this did not reproduce. A late re-verification (e.g. at +60 s) would catch it, but without a
  reproducible cause it is left as an observation, not a change.
- ThreadPilot overhead during the session: 0.08 % of one core.
- Assetto Corsa Rally was not run (same rule shape as ACC).

## Protected games

See `docs/design/protected-processes.md`. Change made: a handle that is granted and then refused with
`ERROR_ACCESS_DENIED` is now reported as anti-cheat/anti-tamper protection instead of "may require administrator
rights" (verified live on Call of Duty).

## Changes on this branch from the audit

| Commit | Effect |
|---|---|
| Find main windows with one EnumWindows pass per process list refresh | ≈2 ms → 0.02 ms of window lookups per refresh |
| Report a refused granted handle as protection, not missing admin rights | correct message for Ricochet-protected games, in Activity too |
