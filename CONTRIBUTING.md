# Contributing

Use the bug report or feature request form for actionable changes.
Installation, usage and design questions belong in [Discussions](https://github.com/sharafutdinovdi/revit-devloader/discussions).
Bug reports include the Revit year, loader version, installation method, feed source, reproduction steps and log excerpt.
Remove credentials and private paths from logs before sharing them.
This project follows the [Contributor Covenant](CODE_OF_CONDUCT.md).

## Pull requests

1. Fork the repository and create a branch from `main`.
2. Keep each change focused and use Conventional Commits, such as `fix(addin): handle an unavailable feed` or `docs: clarify installer usage`.
3. Run the applicable checks below and update tests and documentation for changed behavior.
4. Add a line under `## [Unreleased]` in [CHANGELOG.md](CHANGELOG.md) for every user-visible change.
5. Open a PR against `main` with a Conventional Commit title, complete the PR template and link the issue.
6. Attach a screenshot or recording for any UI or ribbon change in the template's validation section.

Screenshots and recordings can be dragged into or pasted into the issue or PR text area.
For a visible bug, attach a screenshot of the dialog or ribbon and remove private data before uploading.
The PR checklist records build results, test and documentation changes, visual evidence and the absence of secrets.
The first issue and first PR receive a welcome message with contribution guidance.
Path labels identify the affected areas.
Issues and PRs become stale after 60 days without activity and close 14 days later; `pinned` and `bug` are exempt.

## Build from source

Use Windows and the SDK selected by [global.json](global.json).
The Revit API assemblies are restored from NuGet.
Build both add-in families, run the core tests and package the release layout:

```powershell
.\build\build-all.ps1 -SkipTests
dotnet test tests/RevitDevLoader.Core.Tests -c Release
.\build\installer\package.ps1 -SkipBuild
.\build\installer\build-installers.ps1 -InstallIfMissing
dotnet format RevitDevLoader.sln --verify-no-changes --verbosity minimal
```

The build script produces `build/release/<year>` for Revit 2022-2026; `-Versions 2026` limits it to one year.
Tests run after the build to include the release manifest layout check.
For a faster default-host check, run `dotnet build src/RevitDevLoader.Addin.Legacy -c Release` and `dotnet build src/RevitDevLoader.Addin.Modern -c Release`.
Those configurations target Revit 2023 and 2026.
The ZIP and both setup executables appear under `build/release/packages`; `build-installers.ps1` needs Inno Setup 6 or the `-InstallIfMissing` switch, which installs it through Chocolatey.

To install a local build without the setup executable, extract the ZIP and run its script with Revit closed:

```powershell
Expand-Archive -LiteralPath .\build\release\packages\RevitDevLoader-revit-2022-2026.zip -DestinationPath .\artifacts\installer
& .\artifacts\installer\install.ps1 -RevitVersion 2026
```

A downloaded script may need `Unblock-File` or `Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass` for the current session.
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

## Create your own plugin from the samples

Run from the repository root on Windows:

```powershell
Copy-Item .\samples\HelloPlugin .\samples\MyPlugin -Recurse
Rename-Item .\samples\MyPlugin\HelloPlugin.csproj MyPlugin.csproj
notepad .\samples\MyPlugin\HelloCommand.cs
notepad .\samples\MyPlugin\plugin.json
.\tools\feed\Build-Package.ps1 -Project .\samples\MyPlugin -Version 1.0.0 -OutputDir .\artifacts\my-plugin
```

In `plugin.json`, set `id` to `my-plugin`, `displayName` to the chosen name and `entry.assembly` to `2026/MyPlugin.dll`.
Keep `commands[0].class` as `HelloPlugin.HelloCommand` unless the C# namespace or class is renamed.
Edit the dialog text in `HelloCommand.cs` and the SVG/PNG artwork under `icons/`.
The project inherits the shared sample build properties and compiles for both Revit years.
The result is `artifacts/my-plugin/my-plugin-1.0.0.zip` with its manifest, DLLs and icons.

Create a release in a repository you own, then publish its channel:

```powershell
$feedRepo = 'OWNER/REPO'
gh release create preview --repo $feedRepo --title 'Plugin preview' --notes 'Plugin packages'
.\tools\feed\Publish-DevLoaderFeed.ps1 -InputDir .\artifacts\my-plugin -Repo $feedRepo -Tag preview
```

Set that repository and `preview` tag in DevLoader Settings, then select **Check for updates > Install**.
For the next release, rebuild with `-Version 1.1.0`, publish again and select **Check for updates > Update**.
See [plugin package format](docs/plugin-package.md) for multiple commands, application packages and assembly selection, and [feed format](docs/feed-format.md) for the registry schema and the `New-DevLoaderPackage.ps1` and `Publish-DevLoaderFeed.ps1` tools.

## Automated checks

PR checks reuse CI to build Legacy net48 hosts for Revit 2022–2024 and Modern net8 hosts for Revit 2025–2026, run the core and release layout tests, and package the ZIP and user and admin setup executables.
Installer smoke tests verify silent installation and removal, manifest paths, component selection, preserved user data and rejection while Revit is running.
The quality job checks the Conventional Commit PR title, validates workflows with `actionlint`, and runs `dotnet format --verify-no-changes` against the solution.
CodeQL analyzes C# with the same SDK setup and build script as CI.
The `main` branch requires a PR, an approving review and passing `CI / test`, `pr-checks` and `CodeQL (csharp)` checks; administrators can bypass these requirements.

After successful PR checks, one build comment links to the Legacy, Modern and Installer artifacts and lists their Revit years.
The Installer artifact contains the ZIP; the separate Installers artifact on the linked run contains the user and admin setup executables.
The comment updates after each successful build of the current PR revision.
Downloads require a GitHub sign-in and expire after 90 days.
Weekly Dependabot PRs cover NuGet and GitHub Actions dependencies.

## Releases

Releases are cut from `main` by a tag; the workflow refuses to publish without a matching changelog section.

1. Move the `## [Unreleased]` entries in [CHANGELOG.md](CHANGELOG.md) into a new `## [X.Y.Z] - YYYY-MM-DD` section and update the comparison links at the bottom.
2. Merge that change, then tag the merge commit: `git tag vX.Y.Z && git push origin vX.Y.Z`.
3. The Release workflow builds all Revit years, runs the tests, packages the ZIP and both setup executables, writes `SHA256SUMS.txt`, and publishes the GitHub release with a Highlights section taken from the changelog, direct install links and the generated list of merged pull requests.
4. The same workflow dispatches the WinGet workflow, which generates manifests and submits them when `WINGET_TOKEN` is configured. Prereleases (`vX.Y.Z-beta.1`) are marked as such and skip WinGet.

## Documentation

Use sentence-case headings and short sentences.
Use real screenshots with private data removed.
Keep build artifacts and credentials outside version control.
Agent-specific instructions live in [AGENTS.md](AGENTS.md).
