# Feed format

## Settings

The loader reads `%LOCALAPPDATA%\RevitDevLoader\settings.properties`.
An example GitHub configuration is:

```properties
feedRepo=OWNER/REPO
feedTag=preview
feedAsset=feed.json
useLocalUpdatesFallback=false
runRetentionCount=3
```

`feedRepo` requires an explicit `feedTag`.
There is no default repository, feed URL or release tag.
The default asset name is `feed.json`.

Feed selection follows this order:

1. `REVITDEVLOADER_FEED_URL` environment variable.
2. `feedUrl` setting.
3. `REVITDEVLOADER_FEED_REPO` environment variable with the configured tag.
4. `feedRepo` setting with the configured tag.

A full GitHub source looks like `github-release://OWNER/REPO/preview/feed.json`.
Local paths, file URIs and HTTP or HTTPS URLs also work as `feedUrl` values.
GitHub sources require `gh` in PATH with access to the repository.
The loader uses CLI authentication and does not store a token in settings.

| Setting | Environment override | Default |
|---|---|---|
| `updatesFolder` | `REVITDEVLOADER_UPDATES_DIR` | `%LOCALAPPDATA%\RevitDevLoader\updates` |
| `testFeedPath` | `REVITDEVLOADER_TEST_FEED` | `%LOCALAPPDATA%\RevitDevLoader\test-feed\feed.json` |
| `runRetentionCount` | `REVITDEVLOADER_RUN_RETENTION` | `3` |
| `useLocalUpdatesFallback` | None | `false` |

`testFeedPath` is stored in the settings model but is not selected automatically by the update source service.
Set `feedUrl` to that path to use a local feed.
Local ZIP fallback also activates when the selected feed is missing or fails.
See [discovery behavior](how-it-works.md#discovery).

## Feed JSON

[DevUpdateFeed](../src/RevitDevLoader.Core/DevUpdateFeed.cs) defines the serialized fields.
The supported feed schema version is `3`; older schema 1 feeds remain readable.
The top-level object contains `schemaVersion`, `channel`, `generatedUtc` and `plugins`.
Each plugin has `pluginId`, `displayName`, `description`, `icon` and `versions`.
`icon` is an HTTPS or `github-release://` PNG asset URL, or a relative PNG filename in the same release.
The publisher copies the 32x32 package icon to `<pluginId>-icon.png` and writes its HTTPS asset URL.
Installed rows use the installed package icon, matching the ribbon; uninstalled rows load the feed asset.
The catalog accepts square PNGs of at least 32 pixels, including older 64px icons.
An unavailable image falls back to the display-name initial on the existing deterministic palette.
Descriptions appear as the catalog name tooltip.

Each version supplies:

| Field | Meaning |
|---|---|
| `releaseId` | Release identifier used for cache and run paths. |
| `assemblyVersion` | Displayed assembly version. |
| `createdUtc` | Tie breaker for releases with equal or unparseable versions. |
| `supportedRevit` | Revit years as strings. |
| `url` | Archive path or URL, absolute or relative to the feed. |
| `sha256` | SHA-256 of the ZIP bytes. An absent hash disables verification. |
| `size` | Archive size in bytes. A nonpositive value disables size verification. |
| `mainAssembly` | Main DLL filename inside each year folder. |
| `pluginType` | `command` or `application`. Omission means `command`. |
| `commandType` | Full command class name for a command payload. |
| `applicationClass` | Full application class name for an application payload. |

The parser skips entries missing their release ID, assembly version, assembly name, URL or required entry point.
A package must list at least one Revit year.
The highest numeric `releaseId` compatible with the running Revit year is selected first.
Numeric versions accept an optional `v` prefix and two to four components; missing components compare as zero.
For nonnumeric release IDs, numeric `assemblyVersion` determines update ordering.
Equal versions offer Reinstall; a greater feed version offers Update and displays the installed-to-available version pair.
An older feed version offers no install action.
The catalog first shows saved feed data; Check for updates fetches the current feed.
With no saved packages, opening the catalog checks automatically.
An unknown `pluginType` or an empty `supportedRevit` list fails conversion of the feed rather than skipping that entry.
Missing or unparseable `createdUtc` values use the current UTC time.
A newer feed schema produces a warning in the source result and is still parsed.

[samples/feed.json](../samples/feed.json) shows a complete feed.
[samples/manifest.json](../samples/manifest.json) shows one `versions[]` entry with a placeholder owner and repository.
It is a JSON fragment for the feed, not an installed registry manifest.
Replace the sample URL, hash and size with values from a real package.

## Package ZIP

New packages use [plugin.json v2](plugin-package.md) at the archive root with `<year>/` DLL folders and `icons/`.
The feed schema and package schema are independent: feed 3 describes package 2.
Legacy `release-info.properties` packages under `payload/<year>/` remain supported, including properties schema 1 and 2.
Their root `icon.png` remains usable.
The current publisher accepts historical properties schema 2 and new `plugin.json` packages.
Local discovery scans `*.zip` files and ignores unreadable archives.

## Installed registry manifest

Installed manifests use `.devmanifest` key-value files, not JSON.
[DevManifestSerializer](../src/RevitDevLoader.Core/DevManifestSerializer.cs) defines their keys.
The registry writes them during installation.
They contain machine-specific run and assembly paths and are not portable feed assets.
The registry format has no `schemaVersion` field.
Its identity key is `pluginName`, corresponding to the feed's `pluginId`.
Required keys are `pluginName`, `displayName`, `updatedUtc` and at least one `version.<year>.assemblyPath` entry.
Command entries require `commandType`; application entries require `applicationClass` and `pluginType=application`.
An omitted `pluginType` means `command`.
Optional metadata keys are `releaseId`, `assemblyVersion`, `runRoot`, `packagePath` and `commandSlot`.
The command slot range is 1-20; each declared command consumes one slot and applications do not receive a slot.
Additional `iconPath`, `descriptionBase64` and `commandsBase64` keys retain package presentation and per-command slot bindings.
Keys are case-insensitive and the last duplicate key wins.
Unknown keys are ignored.

## Package and publish

Build a sample package with `.\tools\feed\Build-Package.ps1 -Project .\samples\HelloPlugin`.
For existing compiled year folders, create a package on Windows:

```powershell
.\tools\feed\New-DevLoaderPackage.ps1 `
  -PayloadFolder C:\payload `
  -PluginId SamplePlugin `
  -Version 0.1.0 `
  -MainAssembly SamplePlugin.dll `
  -EntryPoint SamplePlugin.Commands.RunCommand `
  -Icon C:\payload\icon.png `
  -Description "Shows the sample command"
```

The icon must be 32x32 PNG.
`-ManifestPath` supplies a complete template with multiple commands and an adjacent icons folder.
The package version comes from `-Version`.
It excludes PDB files and refuses to replace an existing package of the same name.
The returned object includes the archive path, SHA-256 and byte size.
Use `-PluginType application` for an application entry point.

Generate and inspect the feed before uploading:

```powershell
.\tools\feed\Publish-DevLoaderFeed.ps1 `
  -InputDir .\artifacts\packages `
  -Repo OWNER/REPO `
  -Tag preview `
  -DryRun
```

The publisher extracts package icons as `<pluginId>-icon.png` assets and writes each plugin's `icon` field.
For multiple packages of a plugin, the most recently created package carrying an icon supplies the asset.
Packages and icons upload before `feed.json`.
The publisher reads the packages in `InputDir` and includes each unique plugin and release pair.
It writes `artifacts/feed/feed.json` with relative ZIP asset names, absolute icon asset URLs and calculated hashes and sizes.
A repeated plugin and release pair is rejected.
`-DryRun` generates the feed and prints the upload command without invoking GitHub CLI.

Create the target GitHub release separately and authenticate with `gh auth login`.
Run the publisher without `-DryRun` to upload packages and `feed.json` to that release.
Uploads use `--clobber` and replace same-named assets.
Use a new version for changed payload bytes to avoid stale package caches.

## Catalog operations

Command installation adds or re-enables each declared button immediately in Add-Ins > DevLoader.
Updating reuses the assigned command slot and the runner reads the current registry entry on every click.
Uninstall removes registration and hides and disables the command button; Revit exposes visibility and enabled flags but no ribbon-item removal method.
Loaded assemblies remain in the process until Revit exits.
Application installation and removal require a Revit restart to load or unload application behavior.
Run folders remain after uninstall and participate in the configured retention cleanup on subsequent installations.
Open folder opens the installed run root in Explorer.
Successful operations appear inline; failures remain modal.
Conventional installations outside the loader show Managed outside DevLoader and offer no install or uninstall action.
A command may share a DLL with a registered DevLoader application; running the command does not invoke application startup.

Movement: no signature or animations, instant row-state changes, static icons, standard WPF hover/pressed states and a visible keyboard focus border.
No animation dependency or code is included (0 KB); system reduced-motion settings do not change this behavior.

## Demo channel

The `demo-feed` release initially lists Hello Plugin, Element Counter and Level Lister at 1.0.0.
All support Revit 2025 and 2026.
`hello-plugin-1.1.0.zip` is uploaded but absent from the initial `feed.json`.
`feed-with-update.json` adds Hello 1.1.0 while preserving its 1.0.0 version.

After installing and running Hello 1.0.0, replace the feed asset from a temporary directory:

```powershell
gh release download demo-feed --repo sharafutdinovdi/revit-devloader --pattern feed-with-update.json
Copy-Item .\feed-with-update.json .\feed.json
gh release upload demo-feed .\feed.json --repo sharafutdinovdi/revit-devloader --clobber
```

In the manager, select **Check for updates**, then **Update** on Hello Plugin.
Close the manager to run the updated command, then reopen it and select **Uninstall**.
The row returns to Install and its ribbon button is hidden and disabled.
The initial disk state does not alter an already loaded host; staging a host under `RevitDevLoader.next` requires the owner to activate it with Revit closed.
