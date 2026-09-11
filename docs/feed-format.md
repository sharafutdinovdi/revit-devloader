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

Each version supplies:

| Field | Meaning |
|---|---|
| `releaseId` | Release identifier used for cache and run paths. |
| `assemblyVersion` | Displayed assembly version. |
| `createdUtc` | Timestamp used to order available releases. |
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
payload/2024/SamplePlugin.dll
payload/2026/SamplePlugin.dll
```

Other files under a year folder are extracted with its main assembly.
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
An omitted or invalid schema number is interpreted as version `1`.
Package discovery records the schema number without rejecting newer versions.
The publishing script requires schema `2`.
Local discovery scans `*DevPayload*.zip` files in the updates directory and ignores unreadable packages.

## Installed registry manifest

Installed manifests use `.devmanifest` key-value files, not JSON.
[DevManifestSerializer](../src/RevitDevLoader.Core/DevManifestSerializer.cs) defines their keys.
The registry writes them during installation.
They contain machine-specific run and assembly paths and are not portable feed assets.

## Package and publish

Prepare a payload folder containing year directories and their compiled plugin files.
Create a package on Windows:

```powershell
.\tools\feed\New-DevLoaderPackage.ps1 `
  -PayloadFolder C:\payload `
  -PluginId SamplePlugin `
  -Version 0.1.0 `
  -MainAssembly SamplePlugin.dll `
  -EntryPoint SamplePlugin.Commands.RunCommand
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

The publisher reads the packages in `InputDir` and includes each unique plugin and release pair.
It writes `artifacts/feed/feed.json` with relative asset names and calculated hashes and sizes.
A repeated plugin and release pair is rejected.
`-DryRun` generates the feed and prints the upload command without invoking GitHub CLI.

Create the target GitHub release separately and authenticate with `gh auth login`.
Run the publisher without `-DryRun` to upload packages and `feed.json` to that release.
Uploads use `--clobber` and replace same-named assets.
Use a new version for changed payload bytes to avoid stale package caches.
