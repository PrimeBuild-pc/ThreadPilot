# Protected processes: what ThreadPilot does, what it will not do, and why

Some games run a kernel-mode anti-cheat or anti-tamper driver (Ricochet in Call of Duty, Vanguard, EasyAntiCheat,
BattlEye, FACEIT…). They can make `SetProcessDefaultCpuSets`, `SetProcessAffinityMask`, `SetPriorityClass` or
`SetProcessInformation` fail with `ERROR_ACCESS_DENIED`. This note records how ThreadPilot handles that today, what
other tools do, and the design ThreadPilot follows. Measurements come from a real i9-12900K
(see `docs/validation/`).

## What actually happens (measured)

| Game / protection | OpenProcess (limited rights) | CPU Sets | QoS / efficiency | Priority |
|---|---|---|---|---|
| Call of Duty (Ricochet) | granted | **refused, ERROR_ACCESS_DENIED** after the handle was granted | refused the same way | not tested |
| Arma Reforger (BattlEye) | granted, but `PROCESS_VM_READ` refused (so `Process.MainModule` gave no path) | accepted | accepted | accepted |
| Assetto Corsa Competizione (none) | granted | accepted | accepted | accepted (rule, AboveNormal) |

Two details matter for the design:

1. **The refusal happens on the call, not on the open.** Ricochet lets `OpenProcess` succeed and strips the access
   rights from the handle (an `ObRegisterCallbacks` pre-operation callback), so the error shows up on the call.
   ThreadPilot always runs elevated (`requireAdministrator`), so "you may need administrator rights" was the wrong
   hint. Since commit "Report a refused granted handle as protection…" such failures are reported as
   *appears protected by anti-cheat or process protection; ThreadPilot will not try to bypass it*.
2. **The decision depends on the caller.** During diagnosis the same `SetProcessDefaultCpuSets` call from a
   Microsoft-signed process was accepted, while an unsigned .NET executable got `ERROR_ACCESS_DENIED`. The anti-cheat
   decides who may change its game; administrator rights, `SeDebugPrivilege` or `LocalSystem` do not enter into it.

## How other tools do it

Process Lasso and similar tools use the same documented Win32 calls ThreadPilot uses (`SetProcessAffinityMask`,
`SetProcessDefaultCpuSets`, `SetPriorityClass`, IFEO `PerfOptions`). Such tools typically run an elevated background "governor"
(optionally as a service) and may enable `SeDebugPrivilege` to open processes of other users and services. That helps with
**ordinary** access-denied cases (services, other sessions). It does not help against kernel callbacks that strip
handle rights or against protected processes (PPL): the kernel enforces those whatever the caller's token holds.
Ways that *do* get around them (opening the process before the driver is up, racing process creation in suspended
state, injecting into a trusted process, borrowing a signed binary, using a driver) defeat the anti-cheat's intent
and can get the user's account banned. ThreadPilot does none of them, and this design keeps it that way.

## Design

### 1. Least rights, per operation

- Read with `PROCESS_QUERY_LIMITED_INFORMATION` only (paths via `QueryFullProcessImageName`, not
  `Process.MainModule`, which needs `PROCESS_VM_READ` – the right anti-cheats refuse first).
- Write with the narrowest right the call needs (`PROCESS_SET_LIMITED_INFORMATION` for CPU Sets).
- Never hold a full-access handle to a game for its lifetime (Game Boost holds `SYNCHRONIZE |
  PROCESS_QUERY_LIMITED_INFORMATION` only, to be told when it exits).
- No `SeDebugPrivilege`: ThreadPilot never asks for rights that need it, and some anti-cheats treat a process holding
  it as suspicious.

### 2. Classify, do not retry

| Observation | Classification | User message |
|---|---|---|
| `OpenProcess` refused with 5 | access denied (other session, PPL, service) | "Windows denied this change…" |
| handle granted, call refused with 5 | protected by anti-cheat / anti-tamper | "appears protected… will not try to bypass it" |
| error 87 / process gone | exited | "The process exited…" |
| CPU Sets rejected for another reason | unavailable | CPU Sets unavailable, safe fallback only when possible |

Each Game Boost change is attempted once per process instance; persistent rules have a cooldown. A protected game
therefore produces one Activity line per launch, not one per tick. The native calls return result objects and
the apply paths classify failures instead of throwing, so a refusal is a reported outcome, not a crash.

### 3. Offer what does work

For a protected game ThreadPilot can still:

- move the listed **background** (Steam, browsers, launchers) to other cores and EcoQoS – measured working on Call of
  Duty;
- set a **launch-time priority** with IFEO `PerfOptions\CpuPriorityClass` (already in the app as "registry
  priority"): Windows itself applies it when the process is created, so no handle to the game is needed. It is
  documented as not bypassing protection, and the game may still change its own priority later;
- point the user at the game's own settings when they exist (e.g. Call of Duty's `RendererWorkerCount`).

There is no IFEO equivalent for affinity or CPU Sets, and setting them on the launcher so the game inherits them is
unreliable (anti-cheat bootstrappers create the game process themselves). ThreadPilot does not offer either.

### 4. What a SYSTEM service would and would not change

A `LocalSystem` service would let rules apply before anyone logs on and without the UI process. It would not let
ThreadPilot change a game whose anti-cheat refuses it (see above), and it would add a privileged endpoint that the
user-level UI talks to, which is an elevation-of-privilege surface to secure. ThreadPilot already starts elevated
at logon through a scheduled task. **Recommendation: no service for this purpose.**

### 5. Code signing (open question)

Because the refusal depends on the caller, a signed ThreadPilot *might* be accepted by some anti-cheats. That is the
only legitimate lever, and it is the anti-cheat vendor's decision; it was not tested and is not a reason to sign.

## CPU-bound tuning that stays within these rules

- **Hybrid Intel (P/E):** Game Boost puts the game on the P-core threads and the listed background on the E-cores.
  On the 12900K this removed the game's own work from the E-cores (≈1.7 CPUs of Arma Reforger moved to P threads)
  without a measurable frame-rate change in a GPU-bound scene.
- **One thread per P-core (skip SMT siblings):** expressible as CPU Sets (one logical processor per `CoreIndex` of
  the top class). Measured on Arma Reforger: no benefit. Kept as an idea, not a default.
- **Core 0 isolation:** Windows services many interrupts and DPCs on CPU 0. Keeping the game's busiest work off
  CPU 0/1 could reduce jitter on some systems, but it was not measured here (needs a DPC/ISR latency tool and
  frame-time data); not proposed as a default.
- **Core parking:** with the Ultimate Performance plan no core is parked, so CPU Sets are never pointed at parked
  cores. On Balanced, parked P-cores wake on demand; ThreadPilot's "prevent core parking" tweak exists for users who
  want it. No change needed in Game Boost.
- **AMD with several CCDs:** already handled by Game Boost's AMD policies (V-Cache CCD for the game, or background
  off CCD0); unchanged.
