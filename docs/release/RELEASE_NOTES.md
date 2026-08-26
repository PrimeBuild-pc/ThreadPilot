## ThreadPilot v1.7.1

A patch release that makes the CPU affinity feature do what its name says. A user reported that applying a rule did nothing to a process's affinity. They were right, for several independent reasons.

### The headline fix

Applying a saved rule, or a core mask from the Rules tab, now changes the affinity mask Windows actually enforces.

Two defects combined to hide this. `SetProcessorAffinity` silently substituted a Windows CPU Sets hint for the hard affinity write it was asked to perform, while the caller read the affinity back and compared it — so the apply always reported a verification failure and the affinity was never changed. Separately, the shipped default assignment mode was `ThreadPilot automatic`, which applies only CPU Sets: a soft scheduling preference that leaves the affinity mask in Task Manager untouched, reported to the user as "Affinity applied successfully".

The default is now `Affinity Mask`, and a CPU Sets result says plainly that it is a soft preference and that Task Manager will not reflect it. Rules saved before this release keep the mode they were saved with; open the rule and pick `Affinity Mask` if you want a hard, visible restriction.

### The controls were not on screen

The CPU assignment mode picker and the Apply CPU Assignment button, both added in v1.6.0, were placed in a side panel that had been collapsed since v1.2.0. Neither had ever been reachable in a shipped build: the only way to apply affinity was the process-list context menu, and the only way to change the assignment mode was the default in Settings. That is why the reporting user had no way to reach the control that would have fixed their problem.

Both now live in the Advanced Affinity Picker, which opens expanded. The collapsed legacy panel is removed.

### Also fixed

- "Save Current Settings as Rule" no longer saves a rule pinned to every CPU. It captured the core selection only while edits were staged, and otherwise fell back to a process affinity that the soft modes never changed.
- `Automatic` mode no longer installs CPU Sets that a pre-existing hard affinity prevents Windows from honouring. Windows never schedules a process outside its affinity mask, whatever CPU Sets are set, yet the read-back still verified. It now applies the hard affinity, which can replace the restriction.
- Saved rules resolve CPU Sets against the current topology instead of trusting the CPU Set IDs stored when the rule was created. Those IDs are opaque and can name a different processor after a hardware change; a stale rule now reports an invalid topology instead of silently pinning the wrong CPUs.
- Affinity masks that include CPU 63 are no longer discarded. They are negative as signed 64-bit values and were being rejected by magnitude comparisons.

### Interface

- The per-CPU cells in the Advanced Affinity Picker are read-only chips instead of checkboxes, with a hint explaining that a selection is staged through the pending core mask. They were never clickable; they only looked it. The chips keep their accessible name and report selected, not selected, or unavailable.
- System tweak toggles are larger and state ON or OFF inside the track, replacing the unlabelled 40x20 switch. The label is localized and widens the control rather than being clipped.
- Saving a rule from the process context menu now explains that the rule is re-applied automatically when the process next starts, and that these rules are separate from the Rules tab, which manages process-to-power-plan associations.

### Localization

- Corrected "core mask" mistranslations in the Italian, Spanish, French and Russian locales, and German phrasing for the Rules tab and power plans.

### Notes

- Memory Integrity, hardware-accelerated GPU scheduling, and windowed-game optimizations still show a link to the relevant Windows page instead of a toggle. They are deliberately left to Windows.
- Applying affinity to a process protected by anti-cheat still fails, by design. ThreadPilot reports it and does not attempt to bypass protection.

### Verification

- 707 unit tests, including new regression tests that fail against the previous implementation.
- The affinity fix was verified end to end against a live process: requesting cores 0-1 now moves the process from `0xFFFF` to `0x3`, where it previously reported a failure and stayed at `0xFFFF`.
