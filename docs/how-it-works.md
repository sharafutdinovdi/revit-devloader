# How it works

## Discovery

The built-in catalog is empty.
[DevPluginStatusService](../src/RevitDevLoader.Core/DevPluginStatusService.cs) combines packages with registered plugins.
It selects the latest compatible package by creation time.
Command plugins receive one of the slots defined by `DevPluginRegistry.CommandSlotCount`.
Application plugins do not consume command slots.

[DevUpdateSourceService](../src/RevitDevLoader.Core/DevUpdateSourceService.cs) reads the configured feed.
It adds local packages when `useLocalUpdatesFallback=true`.
It also scans local packages when the feed is absent or fails, even when that setting is false.
At window startup, cached rows can include local packages when the cached feed has no entries.
A duplicate plugin and release pair prefers the feed entry.

## Cache and verification

[DevUpdateFeedReader](../src/RevitDevLoader.Core/DevUpdateFeedReader.cs) accepts file paths, file URIs, HTTP, HTTPS and GitHub release URIs.
GitHub downloads use the locally authenticated `gh` executable.
The GitHub feed reader can reuse a cached asset after a download failure.
HTTP and local feed reads do not use that GitHub cache path.

[DevUpdatePackageCache](../src/RevitDevLoader.Core/DevUpdatePackageCache.cs) prepares a local package before installation.
It rejects a size mismatch when the advertised size is positive.
It rejects a SHA-256 mismatch when a hash is present.
An absent hash disables the hash check.
The check covers the archive bytes and does not authenticate the publisher.

Downloaded packages are stored under `%LOCALAPPDATA%\RevitDevLoader\cache`.
Existing cached packages are verified again before use.
The default local layout is:

```text
%LOCALAPPDATA%\RevitDevLoader\
  settings.properties
  updates\*DevPayload*.zip
  test-feed\feed.json
  cache\<pluginId>\<pluginId>-DevPayload-<releaseId>.zip
  cache\github-release\<owner_repo>\<tag>\<asset>
  plugins\<pluginId>.devmanifest
  plugins\<pluginId>\runs\<releaseId>\<year>\
  Logs\DevLoader-<date>.log
```

The GitHub feed cache replaces slashes in repository and tag names with underscores.
`test-feed` is a configurable path, not an automatically selected source.
Local packages are read at their original path and are not copied into the download cache.

## Installation and registry

[DevPayloadInstaller](../src/RevitDevLoader.Core/DevPayloadInstaller.cs) reads `release-info.properties` from the ZIP.
It checks that every requested Revit year has the declared main assembly.
Files are extracted into:

```text
%LOCALAPPDATA%\RevitDevLoader\plugins\<pluginId>\runs\<releaseId>\<year>\
```

If the release folder already exists, the installer tries suffixes from `-r2` through `-r999`.
An existing run is never selected as the destination for a new install.
These run folders are immutable by installation convention.
They are ordinary writable directories on disk.

The registry stores `<pluginId>.devmanifest` beside the plugin directories.
Its key-value content records the command slot, run path and assembly path for each installed year.
Registry writes use a temporary sibling file followed by a move or replacement.
A failed extraction can leave a partial run directory.
The whole installation is not a filesystem transaction.

Application payloads also create `%APPDATA%\Autodesk\Revit\Addins\<year>\<pluginId>.addin`.
The application identity is derived from the plugin ID with SHA-256.
An existing manifest is replaceable only when its assembly path points into the managed plugin directory.
Application loading takes effect on the next Revit start.

## Path checks

Extraction normalizes ZIP entry separators and resolves each destination to a full path.
The destination must begin with the full extraction root plus a directory separator.
An entry that escapes the extraction root throws `DevManifestException`.
Plugin IDs and release IDs are checked for invalid filename characters and separators.
The package cache also checks its resulting path against the cache root.

These checks apply to the paths shown in the implementation.
They are not a sandbox for loaded code.
Feeds, local settings and payload assemblies must come from trusted sources.

## Command loading

[RunPluginCommandBase](../src/RevitDevLoader.Addin.Shared/Commands/RunPluginCommandBase.cs) resolves its slot through the registry.
[ShadowCopyCommandRunner](../src/RevitDevLoader.Addin.Shared/Infrastructure/ShadowCopyCommandRunner.cs) attaches an assembly resolver for the command call.
The main assembly is loaded from its run path with `Assembly.LoadFile`.
Dependencies are resolved from the same directory when the runtime invokes that resolver.
The runner creates the declared command type and invokes its public `Execute` method.
It removes the resolver after the call.

The loader does not unload assemblies or clear plugin static state.
Dependency identity conflicts and conventional installations can still require a Revit restart.
The manager detects matching conventional `.addin` files and displays a warning.

## Retention and removal

The manager passes `runRetentionCount` to the installer.
The default is three runs.
Cleanup preserves the current run when it exists and fills the remaining retention slots by directory modification time.
Equal timestamps are ordered by folder name.
The limit applies to all runs of a plugin, not separately to each Revit year; older runs referenced by another installed year are not separately protected.
Deletion failures are reported as skipped folders.

Removing a plugin deletes its registry entry and hides its command button.
For application payloads, removal deletes owned `.addin` files.
It preserves a manifest that points outside the managed plugin directory.
Removal does not unload code already present in Revit or delete all old run folders.

## Logs

Logs are written under `%LOCALAPPDATA%\RevitDevLoader\Logs`.
The logger falls back to a temporary directory if that folder is unavailable.
Files roll daily and at 10 MiB, with a retention limit of 14 files.
Log messages can contain local paths and feed locations.
