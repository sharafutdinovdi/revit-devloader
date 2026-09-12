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
The supported feed schema version is `1`.
The top-level object contains `schemaVersion`, `channel`, `generatedUtc` and `plugins`.
Each plugin has `pluginId`, `displayName` and `versions`.
Optional `icon` is a PNG asset filename in the same release, for example `"icon": "sample-icon.png"`.
It must be square and at least 64x64 pixels; paths and external URLs are rejected.
The catalog displays it at 40x40 with rounded corners.
If the asset is absent or unreadable, an installed package's root `icon.png` is used when available.
Otherwise the first display-name letter appears on a fixed eight-color palette selected deterministically from `pluginId`.

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

## Payload ZIP

The supported payload schema version is `2`.
Each archive contains:

```text
release-info.properties
icon.png                     # optional
payload/2024/SamplePlugin.dll
payload/2026/SamplePlugin.dll
```

Other files under a year folder are extracted with its main assembly.
The optional root `icon.png` is extracted to the run root.
The package metadata uses key-value lines:

```properties
schemaVersion=2
pluginId=SamplePlugin
displayName=Sample plugin
releaseId=0.1.0
assemblyVersion=1.0.0.0
createdUtc=2026-09-11T00:00:00Z
pluginType=command
commandType=SamplePlugin.Commands.RunCommand
mainAssembly=SamplePlugin.dll
versions=2024,2026
```

For an application payload, use `pluginType=application` and replace `commandType` with `applicationClass`.
The reader accepts `pluginName` as a fallback for `pluginId`.
An omitted or non-integer schema number is interpreted as version `1`.
Other integer values are retained as supplied.
Package discovery records the schema number without rejecting newer versions.
The publishing script requires schema `2`.
Local discovery scans `*DevPayload*.zip` files in the updates directory and ignores unreadable packages.

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
The command slot range is 1-20; applications do not receive a slot.
Keys are case-insensitive and the last duplicate key wins.
Unknown keys are ignored.

## Package and publish

Prepare a payload folder containing year directories and their compiled plugin files.
Create a package on Windows:

```powershell
.\tools\feed\New-DevLoaderPackage.ps1 `
  -PayloadFolder C:\payload `
  -PluginId SamplePlugin `
  -Version 0.1.0 `
  -MainAssembly SamplePlugin.dll `
  -EntryPoint SamplePlugin.Commands.RunCommand `
  -Icon C:\payload\icon.png
```

The script reads the assembly version from the newest year folder unless `-AssemblyVersion` is supplied.
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
It writes `artifacts/feed/feed.json` with relative asset names and calculated hashes and sizes.
A repeated plugin and release pair is rejected.
`-DryRun` generates the feed and prints the upload command without invoking GitHub CLI.

Create the target GitHub release separately and authenticate with `gh auth login`.
Run the publisher without `-DryRun` to upload packages and `feed.json` to that release.
Uploads use `--clobber` and replace same-named assets.
Use a new version for changed payload bytes to avoid stale package caches.

## Catalog operations

Command installation adds or re-enables its button immediately in Add-Ins > DevLoader.
Updating reuses the assigned command slot and the runner reads the current registry entry on every click.
Uninstall removes registration and hides and disables the command button; Revit exposes visibility and enabled flags but no ribbon-item removal method.
Loaded assemblies remain in the process until Revit exits.
Application installation and removal require a Revit restart to load or unload application behavior.
Run folders remain after uninstall and participate in the configured retention cleanup on subsequent installations.
Open folder opens the installed run root in Explorer.
Successful operations appear inline; failures remain modal.

Movement: no signature or animations, instant row-state changes, static icons, standard WPF hover/pressed states and a visible keyboard focus border.
No animation dependency or code is included (0 KB); system reduced-motion settings do not change this behavior.
