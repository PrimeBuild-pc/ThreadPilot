## ThreadPilot v1.7.2

A follow-up to v1.7.1 that closes the gap which stopped that release reaching the people who needed it.

### What was still wrong

v1.7.1 changed the shipped default CPU assignment mode from `ThreadPilot automatic` to `Affinity Mask`, because Automatic applies only Windows CPU Sets: a soft scheduling preference that leaves the affinity mask in Task Manager unchanged.

A new default only applies to a profile that has no stored value. Everyone upgrading had one, so they kept `Automatic` and kept seeing an affinity feature that appeared to do nothing. Verified against the published 1.7.1 build: a fresh profile started on `Affinity Mask`, an existing profile stayed on `Automatic`.

### What 1.7.2 does

- **A one-time migration** moves a stored `Automatic` to `Affinity Mask` on first launch, and records that it ran.
- **A one-time notice**, styled like the rest of the app and shown in the selected language, explains that the mode changed and why, and offers a shortcut to Settings. It appears only for profiles the migration actually moved, and only until acknowledged.
- Choosing `ThreadPilot automatic` again afterwards is respected. The migration never runs twice and never overrides a later deliberate choice.
- Fresh installations already ship with `Affinity Mask`, so they migrate nothing and see no notice.

### Verification

718 unit tests, including migration coverage for an upgraded profile, a fresh profile, every non-Automatic mode, idempotency, and a deliberate return to Automatic after the notice.
