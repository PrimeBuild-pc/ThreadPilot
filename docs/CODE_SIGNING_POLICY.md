# Code-Signing Policy

Free code signing provided by [SignPath.io](https://signpath.io), certificate by [SignPath Foundation](https://signpath.org).

ThreadPilot releases remain unsigned until the SignPath Foundation application is approved and the integration is enabled.

## Scope

Only official ThreadPilot release executables and installers produced from the public GitHub repository may be signed. Pull-request builds, local builds, forks, scripts, and third-party binaries are not signed by the project.

## Authorized releases

- A signed build must originate from a protected release tag in the official repository.
- The commit must pass the repository's required build, test, analyzer, and packaging checks.
- Signing requests must be created by the GitHub release workflow through the approved SignPath integration.
- Maintainer or SignPath approval is required wherever configured by the signing policy.
- Checksums are generated only after signing and are published with the release.

## Team roles and approval

The current repository team consists of one maintainer:

- **Authors:** `@PrimeBuild-pc` may contribute directly to the official repository.
- **Reviewers:** `@PrimeBuild-pc` reviews contributions from people who are not Authors.
- **Approvers:** `@PrimeBuild-pc` manually approves or rejects every SignPath signing request.

No signing request is auto-approved. If another maintainer is added, this section and `.github/CODEOWNERS` must be updated before granting repository or SignPath access. A person must not approve an unreviewed artifact whose source they cannot trace to the approved release commit.

## Account security

Every Author, Reviewer, and Approver must enable multi-factor authentication on both GitHub and SignPath before access is granted and must keep it enabled. MFA status is verified manually when access is created and during each release-access review; it is not inferred from repository API output. Accounts that no longer satisfy this requirement are removed from the project until MFA is restored.

## Key protection

ThreadPilot maintainers do not download, export, or commit the signing private key. SignPath controls the signing key. Integration credentials are restricted to a protected GitHub environment and are never exposed to pull requests from forks.

## Verification and transparency

Release notes identify whether artifacts are signed. Users can inspect the Authenticode signature with `Get-AuthenticodeSignature` and compare the published SHA-256 checksums. The source revision used for a release is represented by its public Git tag.

## Revocation and incidents

If credentials are exposed, an unauthorized signing request is suspected, or a signed artifact differs from the published source build, maintainers will disable release signing, revoke or rotate affected credentials, withdraw the artifact, publish an incident notice, and coordinate certificate revocation with SignPath when necessary.

## Policy changes

Changes to signing scope, identity, or approval requirements are reviewed as repository changes before taking effect.
