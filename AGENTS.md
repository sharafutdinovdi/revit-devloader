# Agent instructions

Rules for coding agents working in this repository. Humans follow [CONTRIBUTING.md](CONTRIBUTING.md); this file adds what an agent needs to act without asking.

## What this project is

Revit DevLoader installs versioned test plugins for Autodesk Revit 2022-2026 from a feed and runs each version from its own folder. Two audiences: add-in developers who publish builds to a feed, and testers who install and update them from the Revit ribbon.

## Repository map

| Path | Contents |
|---|---|
| `src/RevitDevLoader.Core` | Feed parsing, package cache, registry, settings, manager state. No Revit API. Targets `net48;net8.0`. |
| `src/RevitDevLoader.Addin.Shared` | Add-in code shared by both hosts: ribbon, command slots, manager window. |
| `src/RevitDevLoader.Addin.Legacy` | Revit 2022-2024 host, `net48`. |
| `src/RevitDevLoader.Addin.Modern` | Revit 2025-2026 host, `net8.0-windows`. |
| `tests/RevitDevLoader.Core.Tests` | xUnit tests for Core plus the release layout check. Run without Revit. |
| `build/` | `build-all.ps1`, `installer/` (ZIP packaging, Inno Setup script, installer builder), `winget/` (manifest templates). |
| `samples/` | Three sample plugins and their shared build properties. |
| `tools/feed/` | PowerShell tools to build packages and publish feeds. |
| `docs/` | How it works, feed format, plugin package format, roadmap, screenshots. |
| `.github/` | Workflows (CI, PR checks, CodeQL, release, WinGet, shared community callers), issue forms, PR template. |

`build/release/**` is generated output and is never edited or committed.

## Commands

Install hooks with `pre-commit install`; run local checks with `pre-commit run --all-files`.
The solution formatting hook requires Windows; macOS and Linux results rely on PR checks for that hook.

Windows, with the SDK from `global.json`:

```powershell
.\build\build-all.ps1 -SkipTests
dotnet test tests/RevitDevLoader.Core.Tests -c Release
.\build\installer\package.ps1 -SkipBuild
.\build\installer\build-installers.ps1 -InstallIfMissing
dotnet format RevitDevLoader.sln --verify-no-changes --verbosity minimal
```

Any OS, for the core tests alone: `dotnet test tests/RevitDevLoader.Core.Tests -c Release`.
After editing a workflow: `actionlint` from the repository root.
Windows-only steps (Revit builds, Inno Setup, installer smoke tests) run in GitHub Actions; a pull request's checks are the oracle for them.

## Conventions

- Conventional Commits for commits and PR titles: `feat(addin): ...`, `fix(core): ...`, `ci: ...`, `docs: ...`.
- English everywhere. Sentence-case headings, short sentences, no em dashes.
- Every behavior change comes with a test in `tests/RevitDevLoader.Core.Tests` and a Conventional Commit title that describes the change.
- Follow `.editorconfig`; run `dotnet format` before committing.
- Revit API types stay in the add-in projects. Core and tests never reference `RevitAPI.dll` or `RevitAPIUI.dll`.
- No Autodesk assemblies in any artifact; the packaging scripts and CI assert this.
- Documentation links are relative and must resolve; PR checks and the docs build fail on broken links.

## Boundaries

- Do not change the installer `AppId` GUIDs in `build/installer/RevitDevLoader.iss`; Windows uses them to find existing installations.
- Do not add a default feed, default repository or default release tag; users configure their own.
- Do not weaken the path checks in package extraction or the size and SHA-256 verification.
- Do not commit credentials, tokens, private repository names or local paths from logs.
- Do not delete or skip tests to make a build pass.

## Releases

release-please owns `CHANGELOG.md` and `version.txt`; agents never edit them by hand.
Conventional Commit titles determine the release notes and version in the release PR.
A maintainer merges that PR to create the `vX.Y.Z` tag and GitHub release.
The Release workflow builds and uploads the installers, ZIP and checksums, then dispatches WinGet.
Agents never push tags.
