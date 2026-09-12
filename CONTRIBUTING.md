# Contributing

Use the bug report or feature request form for actionable changes.
Installation, usage and design questions belong in [Discussions](https://github.com/sharafutdinovdi/revit-devloader/discussions).
Bug reports include the Revit year, loader version, installation method, feed source, reproduction steps and log excerpt.
Remove credentials and private paths from logs before sharing them.

## Pull requests

1. Fork the repository and create a branch from `main`.
2. Keep each change focused and use Conventional Commits, such as `fix(addin): handle an unavailable feed` or `docs: clarify installer usage`.
3. Run the applicable checks below and update tests and documentation for changed behavior.
4. Open a PR against `main` with a Conventional Commit title, complete the PR template and link the issue.
5. Attach a screenshot or recording for any UI or ribbon change in the template's validation section.

Screenshots and recordings can be dragged into or pasted into the issue or PR text area.
For a visible bug, attach a screenshot of the dialog or ribbon and remove private data before uploading.
The PR checklist records build results, test and documentation changes, visual evidence and the absence of secrets.
The first issue and first PR receive a welcome message with contribution guidance.
Path labels identify the affected areas.
Issues and PRs become stale after 60 days without activity and close 14 days later; `pinned` and `bug` are exempt.

## Development

Use Windows and the SDK selected by [global.json](global.json).
The Revit API assemblies are restored from NuGet.
Build both add-in families and run the core tests before submitting a change:

```powershell
.\build\build-all.ps1 -SkipTests
dotnet test tests/RevitDevLoader.Core.Tests -c Release
.\build\installer\package.ps1 -SkipBuild
dotnet format RevitDevLoader.sln --verify-no-changes --verbosity minimal
```

The build script produces `build/release/<year>` for Revit 2022-2026.
Tests run after the build to include the release manifest layout check.
For a faster default-host check, run `dotnet build src/RevitDevLoader.Addin.Legacy -c Release` and `dotnet build src/RevitDevLoader.Addin.Modern -c Release`.
Those configurations target Revit 2023 and 2026.
Installer ZIPs appear under `build/release/packages`.
Keep `REVITDEVLOADER_FEED_CHECK` unset for ordinary tests; the opt-in feed check installs real payloads.
Keep shared add-in code under `src/RevitDevLoader.Addin.Shared`.
Keep feed and registry contract changes covered by tests.
Describe the user-visible behavior and validation in the pull request.

Formatting follows [`.editorconfig`](.editorconfig).
Run `dotnet format RevitDevLoader.sln` to apply formatting before committing.
Run `actionlint` from the repository root when editing GitHub Actions workflows.

The core tests also run on macOS and Linux with `dotnet test tests/RevitDevLoader.Core.Tests -c Release`.
Release layout coverage requires the Windows build output; Revit UI behavior requires a manual check in Revit.
This repository has no Python server or Python tests.

## Automated checks

PR checks reuse CI to build Legacy net48 hosts for Revit 2022–2024 and Modern net8 hosts for Revit 2025–2026, run the core and release layout tests, and package the installer.
The quality job checks the Conventional Commit PR title, validates workflows with `actionlint`, and runs `dotnet format --verify-no-changes` against the solution.
CodeQL analyzes C# with the same SDK setup and build script as CI.
The `main` branch requires a PR, an approving review and passing `CI / test`, `pr-checks` and `CodeQL (csharp)` checks; administrators can bypass these requirements.

After successful PR checks, one build comment links to the Legacy, Modern and Installer artifacts and lists their Revit years.
The comment updates after each successful build of the current PR revision.
Downloads require a GitHub sign-in and expire after 90 days.
Weekly Dependabot PRs cover NuGet and GitHub Actions dependencies.
Tagged releases use the existing installer workflow and group generated notes by PR labels.

## Documentation

Use sentence-case headings and short sentences.
Use real screenshots with private data removed.
Keep build artifacts and credentials outside version control.
