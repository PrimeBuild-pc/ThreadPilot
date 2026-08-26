## ThreadPilot v1.7.4

A follow-up to 1.7.3, closing the three items left open in its review.

### Custom masks follow a CPU change

Built-in masks were already re-derived when the logical-processor count changed; masks you drew yourself were only flagged. They are now resized too, because most of the decision is not a judgement call: CPUs that appear were not part of a selection made before they existed, so they stay unselected, and removing CPUs is safe while something is still selected.

Two cases are still yours to decide, and ThreadPilot says so in the Masks tab rather than guessing:

- the mask would be left selecting no CPU that still exists;
- a power plan association is using the mask, where a silent resize would change what that automation does.

A different CPU with the same thread count is also detected now, by comparing the topology signature stored with the mask against the running machine - a swap that the logical-processor count alone cannot reveal.

### Saved rules are checked against the CPU you are running

A rule that pins cores which no longer exist was not flagged anywhere; it failed when it was applied, through a path that reported it as the process having exited. The **Saved process rules** tab now names the rules whose cores came from a different chip, and you can set them again from a saved mask without leaving the page.

### A rule's cores can be edited from the Rules page

The per-core picker lives in the Process tab and needs the process to be running, so the cores of a rule for an application you were not currently running were the one thing about that rule you could not change.

The **Saved process rules** editor now shows those cores as readable ranges and can set them from any saved CPU mask, through the same conversion the Process tab uses. The per-core picker stays where it is rather than being duplicated.

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
