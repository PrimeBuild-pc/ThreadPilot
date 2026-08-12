# Bundled Power Plan Licensing Audit

## Result

**Resolved for 1.7.0:** the 89 unverified `.pow` files were removed from the repository and release artifacts.

The files were obtained from public YouTube descriptions or forum posts and were available at no charge. Public availability and zero price do not grant redistribution rights and do not make a component open source. File names that refer to Microsoft or third-party projects are not evidence of authorship, origin, or license.

Under the SignPath Foundation terms, every component of the project must be distributed under an OSI-approved license and the project must not contain proprietary components. ThreadPilot therefore no longer bundles third-party power-plan files.

## Evidence reviewed

- All 89 `.pow` files under `assets/Powerplans`.
- Repository license, notices, documentation, and file history.
- The pull request and commits that introduced or expanded the bundle.
- Build, publish, installer, and release-workflow paths that copy every `.pow` file into release artifacts.

No per-file source URL, author permission, license text, SPDX identifier, or provenance record was found. Every removed file remains classified as **unverified** unless contrary evidence is recorded.

## Implemented remediation

- ThreadPilot retains its local `.pow` import feature.
- Windows plans that users previously imported or activated are not deleted or changed.
- Upgrade installs do not delete old local `.pow` files from users' machines.
- The Power Plans page offers an explicit user-initiated link to the ThreadPilot Discord community for additional plans; no third-party plan is downloaded automatically or included in signed artifacts.

An informal statement such as “free to download/use,” a public post, a video description without license terms, or attribution alone is insufficient.

## Admission checklist for future power plans

- Original source URL and author/rights holder identified.
- Exact OSI-approved license identified and preserved.
- Redistribution and modification are permitted.
- SHA-256 hash recorded for the reviewed file.
- Any attribution or notice requirements are included in the release.
- The file is reviewed before being added to any release asset directory.
