## ThreadPilot v1.7.3

ThreadPilot 1.7.3 focuses on trust: an operation that reports success must remain visible, survive a UI refresh, and preserve the rest of the user's configuration.

### Saved rules no longer lose information

**Apply CPU Assignment and Save as Rule** now updates the CPU portion of an existing rule without discarding its memory priority, I/O priority, identity, creation time, or other stored behavior. The selected-process summary is refreshed before saving so the rule captures the process state Windows actually reports.

Rules are no longer permanent. Open a process's context menu, expand **Rules**, and choose **Delete Saved Rule** to remove the matching rule. ThreadPilot updates the status and selected-process summary immediately.

### CPU assignments are read back from Windows

CPU Sets and Ideal Processor are soft Windows scheduling controls, so they do not change the classic affinity mask shown by Task Manager. Previous builds reapplied the affinity mask to the UI whenever a process was selected, making a successful soft assignment appear to have vanished and risking a later rule being saved with every CPU selected.

The Advanced Affinity Picker now reads the effective CPU Sets or ideal processors from Windows and labels the result explicitly. Process CPU Sets are detected even when the global default or saved rule is `Affinity Mask`. This was verified by applying CPU Sets to a disposable Notepad process, switching away, restarting ThreadPilot, and confirming the same CPU set selection was read back from the operating system.

Successful assignment feedback also remains in the process status instead of being cleared immediately after the operation.

### Existing rules receive the v1.7.2 migration

v1.7.2 migrated the global profile from `ThreadPilot automatic` to `Affinity Mask`, but saved rules had their own stored assignment mode and were not included. v1.7.3 performs a separate, one-time migration for topology-aware rules that still use `Automatic`.

The migration intentionally leaves these rules untouched:

- rules using a deliberately selected mode (`Affinity Mask`, `CPU Sets`, or `Ideal Processor`);
- legacy affinity-mask rules, where Automatic already reaches a real affinity operation;
- rules that do not contain a CPU selection.

The migration flag is stored only after the updated rules are written successfully, so an interrupted save is retried on the next launch.

### Settings now do what they say

- **Reset process changes on exit** is available in Settings in every supported language. It remains enabled by default, preserving the established safety behavior, but can now be disabled when users want applied masks and priorities to remain after ThreadPilot exits.
- **Polling interval** now changes the real Process Management refresh timer, including while ThreadPilot is running.
- **Start minimized** now rewrites an enabled Windows autostart task when its value changes, preventing an old command-line flag from overriding the saved preference.
- Built-in CPU masks are regenerated when the machine's logical-processor count changes, while user-created masks remain untouched.

### Verification

- Release build completed without warnings.
- 739 automated tests passed in Release configuration.
- The elevated Windows 11 application was opened and exercised through real UI Automation: navigation, settings changes and restoration, process refresh, CPU Sets, Ideal Processor, saved-rule creation and deletion, persistent success status, and read-back after restart all passed.
- Temporary rules, settings, processes, and test automation artifacts were removed after the run.

No manual migration or configuration reset is required when upgrading from v1.7.2.
