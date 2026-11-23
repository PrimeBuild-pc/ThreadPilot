# Code signing policy

Free code signing provided by [SignPath.io](https://signpath.io), certificate by [SignPath Foundation](https://signpath.org).

Published ThreadPilot releases are currently unsigned. The GitHub Actions integration has been tested with a self-signed SignPath certificate. Production signing starts after SignPath Foundation issues the release certificate and the integration is enabled.

## Scope

Production signing covers the ThreadPilot Windows executable and Inno Setup installer built from the official public GitHub repository. The portable ZIP contains the signed executable. Third-party binaries and scripts are outside the signing configuration.

## Authorized releases

- A production release must originate from a protected release tag in the official repository.
- The commit must pass the repository's required build, test, analyzer, and packaging checks.
- Signing requests must be created by the GitHub release workflow through the approved SignPath integration.
- A maintainer manually approves each production signing request.
- Checksums are generated only after signing and are published with the release.

## Team roles and approval

The following roles apply to production release signing:

- **Authors:** [PrimeBuild-pc](https://github.com/PrimeBuild-pc) maintains the release source and build workflow.
- **Reviewers:** [PrimeBuild-pc](https://github.com/PrimeBuild-pc) reviews changes proposed by other contributors.
- **Approvers:** [PrimeBuild-pc](https://github.com/PrimeBuild-pc) approves or rejects production signing requests in SignPath.

Production requests require manual approval. The separate test policy uses a self-signed certificate for workflow validation. A person must not approve an artifact whose source they cannot trace to the approved release commit.

## Account security

Every Author, Reviewer, and Approver must enable multi-factor authentication on both GitHub and SignPath before access is granted and must keep it enabled. MFA status is verified manually when access is created and during each release-access review; it is not inferred from repository API output. Accounts that no longer satisfy this requirement are removed from the project until MFA is restored.

## Key protection

SignPath holds the signing key. GitHub Actions stores the token for the SignPath CI user as a repository secret. Signing requests use GitHub's trusted build integration to verify the artifact's origin.

## Privacy

See the [ThreadPilot privacy policy](PRIVACY.md) for what is stored locally and how update requests reach GitHub.

## Verification and transparency

Release notes identify whether artifacts are signed. Users can inspect the Authenticode signature with `Get-AuthenticodeSignature` and compare the published SHA-256 checksums. The source revision used for a release is represented by its public Git tag.

## Revocation and incidents

If credentials are exposed, an unauthorized signing request is suspected, or a signed artifact differs from the published source build, maintainers will disable release signing, revoke or rotate affected credentials, withdraw the artifact, publish an incident notice, and coordinate certificate revocation with SignPath when necessary.

## Policy changes

Changes to signing scope, identity, or approval requirements are reviewed as repository changes before taking effect.
