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

## Pull Request Checklist
- I built the solution successfully in Debug and Release.
- I ran tests and validated impacted features.
- I updated documentation for any user-facing or architectural changes.
- I validated no credentials or secrets were introduced.
- I included risk notes for any changes touching elevation, process control, or power plans.

## Coding Standards
- Follow existing MVVM and DI patterns.
- Prefer async APIs and avoid blocking calls on UI paths.
- Add XML documentation comments for public APIs.
- Keep classes cohesive and avoid mixing UI logic with system logic.

## Testing Expectations
- Add unit tests for new logic when feasible.
- Add or update integration checks for process and power plan workflows.
- Validate behavior on Windows 11; Windows 10 is best effort.
