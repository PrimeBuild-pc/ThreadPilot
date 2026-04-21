# Contributing to ThreadPilot

Thanks for helping improve ThreadPilot.

## Before You Start
- Search existing issues before opening a new one.
- Keep pull requests scoped to a single concern.
- For security issues, do not open a public issue. Follow the process in [SECURITY.md](SECURITY.md).

## Development Setup
1. Install .NET 8 SDK.
2. Clone the repository.
3. Restore and build:
   - dotnet restore ThreadPilot_1.sln
   - dotnet build ThreadPilot_1.sln --configuration Release
4. Run application:
   - dotnet run --project ThreadPilot.csproj --configuration Release

## Branch and Commit Guidelines
- Use short-lived feature branches.
- Use clear commit messages in imperative mood.
- Keep history clean and avoid unrelated formatting churn.

## Branch Policy
- `main` is protected: no direct pushes.
- Pull requests into `main` require at least 1 approval.
- All required CI checks must pass before merge.
- Branch naming must follow one of these prefixes:
   - `feat/`
   - `fix/`
   - `perf/`
   - `chore/`
   - `release/`
- Commit messages should follow Conventional Commits:
   - `feat: ...`
   - `fix: ...`
   - `perf: ...`
   - `docs: ...`
   - `chore: ...`

## Pull Request Checklist
- I built the solution successfully in Debug and Release.
- I ran tests and validated impacted features.
- I updated documentation for any user-facing or architectural changes.
- I validated no credentials or secrets were introduced.
- I included risk notes for any changes touching elevation, process control, or power plans.
- I did not vendor scanner binaries/docs (for example `gitleaks-bin`); security tools must run from CI runtime downloads.

## Coding Standards
- Follow existing MVVM and DI patterns.
- Prefer async APIs and avoid blocking calls on UI paths.
- Add XML documentation comments for public APIs.
- Keep classes cohesive and avoid mixing UI logic with system logic.

## Testing Expectations
- Add unit tests for new logic when feasible.
- Add or update integration checks for process and power plan workflows.
- Validate behavior on Windows 11; Windows 10 is best effort.

## Artifact Cleanup Policy

Before committing, ensure local/generated artifacts are not staged.

- AI workspace artifacts: `.kilo/`, `.roo/`, `.cursor/`, `.continue/`, `.aider*/`
- Build artifacts: `bin/`, `obj/`, `artifacts/` generated outputs
- Logs/temp: `*.log`, `*.tmp`, `checkpoint*/`

If an artifact is accidentally tracked, remove it from index without deleting local copy:

- `git rm --cached <path>`

Then update `.gitignore` when needed.

Optional local enforcement:

- `./build/install-git-hooks.ps1`

This enables the repository pre-commit hook (`.githooks/pre-commit.ps1`) that blocks common artifact patterns and large files.

## Security Tooling Policy

- Do not commit third-party security scanner binaries, archives, or copied upstream documentation.
- Secret scanning and dependency/security tools must be installed at CI runtime from trusted release sources.
- If scanner test vectors trigger GitHub Secret Scanning alerts, remove vendored artifacts and close alerts with documented rationale.
