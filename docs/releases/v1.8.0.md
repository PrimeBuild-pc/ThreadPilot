# ThreadPilot v1.8.0

## Game Boost and Intel hybrid CPUs

- Improved game detection, including Arma Reforger. On a Core i9-12900K, we verified that Game Boost can keep a supported game on P-cores and move selected background apps to E-cores. We also verified that changes are restored when the session ends.
- The Processes page now explains where a CPU Set assignment came from. A new read-only diagnostic shows process defaults and individual thread overrides separately.
- Fixed Windows Game Mode status when Windows is using its default setting. Game Boost now records session decisions and results in Activity and in the diagnostic log.

## System tray, updates, and uninstall

- Redesigned the tray menu for Windows 11 with light and dark themes. The global shortcut to open it is configurable.
- When an update is detected, its entry stays in the tray menu so you can find it again. Installation still asks for confirmation.
- The installer and portable uninstaller offer to remove app data and restore tracked Windows changes. They preserve user exports and report items that need attention. In-place updates keep your settings.
- The process list does less work to find main windows during refresh. Persistent priority rules also get a later verification pass.

The Intel test verified CPU placement and restoration. It did not establish an FPS gain. Protected games may refuse changes to CPU Sets or efficiency settings.

The Windows installer and portable executable are unsigned in this release. SHA-256 checksums are provided with the downloads. [Code signing policy](https://github.com/PrimeBuild-pc/ThreadPilot/blob/main/docs/CODE_SIGNING_POLICY.md)
