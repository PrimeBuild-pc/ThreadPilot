# ThreadPilot v1.7.11

- **Start with Windows is now opt-in.** New installs start with it off. The first launch shows one notice with **Turn on now** and **Open Settings**. If you already saved a choice, it stays as it was.
- **Scheduled tasks only when you ask.** The sign-in task and the task that lets ThreadPilot start without a second UAC prompt are created only while Start with Windows is on. Turning it off removes both.
- **Tasks follow updates.** At startup, if the sign-in task still points at an older install folder (for example after a Microsoft Store update), ThreadPilot registers it again for the current version.
- **Microsoft Store build.** First Store (MSIX) package: no in-app updater, data in `%APPDATA%\ThreadPilot`. Start with Windows uses one non-elevated sign-in task that opens ThreadPilot like the Start menu does, so Windows asks for UAC consent; there is no UAC-free launch task in this edition. What stays on the system after uninstalling is listed in [PRIVACY.md](../PRIVACY.md#uninstalling-threadpilot).

Installer and portable builds are available below with SHA-256 checksums.
