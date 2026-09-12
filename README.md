# Revit DevLoader

Versioned test plugins for Revit, installed from a feed and loaded from separate run folders.

![Status: preview](https://img.shields.io/badge/status-preview-grey?style=flat-square) [![CI](https://img.shields.io/github/actions/workflow/status/sharafutdinovdi/revit-devloader/ci.yml?style=flat-square)](https://github.com/sharafutdinovdi/revit-devloader/actions/workflows/ci.yml) [![CodeQL](https://img.shields.io/github/actions/workflow/status/sharafutdinovdi/revit-devloader/codeql.yml?style=flat-square&label=CodeQL)](https://github.com/sharafutdinovdi/revit-devloader/actions/workflows/codeql.yml) [![Release](https://img.shields.io/github/v/release/sharafutdinovdi/revit-devloader?include_prereleases&style=flat-square)](https://github.com/sharafutdinovdi/revit-devloader/releases) ![Revit 2022-2026](https://img.shields.io/badge/Revit-2022--2026-005FB8?style=flat-square) [![MIT](https://img.shields.io/badge/license-MIT-blue?style=flat-square)](LICENSE)

## What it does

Revit DevLoader installs test plugins from versioned ZIP packages.
The manager discovers packages from a feed and loads installed commands through the Revit ribbon.
Each installation gets a separate run folder and a local registry entry.

## Why

Testing a new plugin build often requires replacing files that Revit has already loaded.
DevLoader keeps each installation in a separate directory.
A feed supplies release metadata and package locations without rebuilding the loader for each plugin.
GitHub Releases can serve private feeds through the locally authenticated GitHub CLI.

## In action

A recording of the catalog, install, update and uninstall flow is being redone and will land here.

## Quick start

Run these commands in PowerShell on Windows with Revit closed.
Install Git and the .NET SDK selected by [global.json](global.json).
Revit is needed to run the add-in; its API references restore from NuGet during the build.

1. Clone, build for Revit 2026 and create the installer ZIP:

   ```powershell
   Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
   git clone https://github.com/sharafutdinovdi/revit-devloader.git
   Set-Location revit-devloader
   .\build\build-all.ps1 -Versions 2026
   .\build\installer\package.ps1 -SkipBuild
   Expand-Archive -LiteralPath .\build\release\packages\RevitDevLoader-revit-2026-2026.zip -DestinationPath .\artifacts\installer
   & .\artifacts\installer\install.ps1 -RevitVersion 2026
   ```

   The execution-policy setting applies only to the current PowerShell session.
   The installer writes `%APPDATA%\Autodesk\Revit\Addins\2026\RevitDevLoader.addin` and the adjacent `RevitDevLoader` folder.
   For another year, change `-Versions`, the ZIP filename and `-RevitVersion` together.
   Omitting `-Versions` builds all configured years.
   Prebuilt folders are also available as **Legacy** and **Modern** artifacts on successful [CI runs](https://github.com/sharafutdinovdi/revit-devloader/actions/workflows/ci.yml).
   Tagged [releases](https://github.com/sharafutdinovdi/revit-devloader/releases) provide an installer ZIP with the same `install.ps1` command.

2. Start Revit and open **Add-Ins > DevLoader > Dev**.
   Open **Settings** and enter your feed repository, release tag and `feed.json` asset name.
   GitHub release feeds require GitHub CLI in PATH and `gh auth login` on that Windows account.
   There is no default feed or release tag.
   A local or HTTPS feed can instead be set with `feedUrl` in `%LOCALAPPDATA%\RevitDevLoader\settings.properties`.

3. Select **Check for updates**, then **Install** on a compatible payload.
   The row changes to installed and a `.devmanifest` file appears under `%LOCALAPPDATA%\RevitDevLoader\plugins`.
   Command payloads receive a ribbon button; close the manager to run it.
   Application payloads load on the next Revit start.

The recorded [demo feed](https://github.com/sharafutdinovdi/revit-devloader/releases/tag/demo-feed) uses repository `sharafutdinovdi/revit-devloader`, tag `demo-feed` and asset `feed.json`.
It contains RevitDayByDay 1.0.0 and a DevLoader 0.1.0 application payload for Revit 2026.
Select RevitDayByDay to reproduce the installation shown above, then restart Revit.
The DevLoader payload demonstrates delivery of an application assembly; installing it alongside the bootstrap can load two copies.
Bootstrap self-update is not implemented.

## How it works

```mermaid
flowchart LR
    Feed[Feed or local ZIP] --> Cache[Package cache]
    Cache --> Verify[Size and SHA-256 checks]
    Verify --> Run[Separate run folder]
    Run --> Registry[Local registry]
    Registry --> Command[Revit command slot]
```

The installer checks extraction paths against the destination folder.
Reinstalls use a new run folder and update the registry without overwriting the previous run.
The cache verifies SHA-256 when the feed supplies a hash and verifies size when it is positive.
Command slots resolve the installed assembly path and invoke the command through reflection.
See [how it works](docs/how-it-works.md) for loading limits and retention behavior.

## Features

| Feature | Behavior |
|---|---|
| Generic catalog | Combines feed packages and registered plugins. |
| GitHub Releases | Uses `gh` authentication for public or private release assets. |
| Package checks | Compares supplied hashes and sizes before installation. |
| Run folders | Preserves existing runs when a release is reinstalled. |
| Command plugins | Assigns a persistent slot and adds a ribbon button. |
| Application plugins | Writes an application `.addin` manifest for the next Revit start. |
| Local fallback | Scans local ZIP packages when the feed is absent or fails. |

## Feed format

A JSON feed describes releases and points to ZIP packages with `release-info.properties` and `payload/<year>/` folders.
See [feed format and publishing](docs/feed-format.md) for schemas, settings, examples and package commands.

## Testing

The xUnit suite covers feed parsing, package extraction, registry writes and run retention.
It also covers settings precedence, assembly path loading and manager row states.
The ordinary test suite does not require Revit.

[Windows CI](https://github.com/sharafutdinovdi/revit-devloader/actions/workflows/ci.yml) builds Revit 2022-2026 before running the suite.
This includes the release manifest layout check.
To run the same build and tests from the repository root on Windows:

```powershell
.\build\build-all.ps1 -SkipTests
dotnet test tests/RevitDevLoader.Core.Tests -c Release
.\build\installer\package.ps1 -SkipBuild
```

The feed delivery smoke test performs work only when `REVITDEVLOADER_FEED_CHECK=1` is set.
It uses the configured feed and installs payloads into the current user's loader directories.
It is not enabled in CI.
Ordinary tests verify contracts without opening Revit; they do not establish live compatibility for every supported year.

## Compatibility

| Component | Configured support |
|---|---|
| Legacy add-in | Revit 2022-2024, `net48`, Windows |
| Modern add-in | Revit 2025-2026, `net8.0-windows`, Windows |
| Core | `net48;net8.0` |
| Tests | `net8.0` |
| Plain Release builds | Revit 2023 for Legacy, Revit 2026 for Modern |
| Build matrix | `Debug.R22` through `Release.R26`, split by add-in family |
| Revit API references | NuGet packages selected by Revit year |

The recording above demonstrates application payload installation in Revit 2026.
Live command execution and the other configured Revit years still need host validation.
See [known gaps](docs/roadmap.md#known-gaps) for the remaining limitations.
Replacing an application payload requires restarting Revit.
Command loading does not unload previously loaded assemblies or reset plugin static state.

## Author and license

Author and maintainer: Dinar Sharafutdinov.
Licensed under [MIT](LICENSE).
See [third-party notices](THIRD-PARTY-NOTICES.md) for dependency licenses.
