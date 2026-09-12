# Plugin packages

A package is a ZIP with `plugin.json` at its root, compiled assemblies under Revit year folders and PNG icons under `icons/`.
The package is independent of the machine that builds or installs it.

```text
hello-plugin-1.0.0.zip
  plugin.json
  icons/hello.png
  icons/hello@16.png
  2025/HelloPlugin.dll
  2026/HelloPlugin.dll
```

## Manifest v2

```json
{
  "schemaVersion": 2,
  "id": "hello-plugin",
  "displayName": "Hello Plugin",
  "version": "1.0.0",
  "description": "Shows a dialog. The smallest possible DevLoader plugin.",
  "author": "Dinar Sharafutdinov",
  "revit": ["2025", "2026"],
  "icon": "icons/hello.png",
  "entry": { "assembly": "2026/HelloPlugin.dll" },
  "commands": [
    {
      "id": "hello",
      "class": "HelloPlugin.HelloCommand",
      "text": "Hello\nPlugin",
      "tooltip": "This is your first plugin",
      "icon": "icons/hello.png"
    }
  ]
}
```

`id` is the stable package identity used in feeds and the installed registry.
`version` identifies an immutable release; publish changed bytes under a new version.
IDs and versions start with an ASCII letter or digit and contain only letters, digits, dots, underscores and hyphens.
`displayName`, supported Revit years, the package icon and `entry.assembly` are required.
`description` and `author` describe the package for users and publishers.

Every `commands[]` entry requires a unique `id`, a full `IExternalCommand` class name and button `text`.
A newline in `text` creates a multiline ribbon label.
`tooltip` defaults to the package display name and an omitted command `icon` uses the package icon.
Command IDs remain stable across releases; the loader preserves their assigned slots when updating.
The catalog displays the package description as the name tooltip.

An application package has no commands and declares `entry.applicationClass` beside `entry.assembly`.
The class implements `IExternalApplication`.
Application packages do not create command buttons and take effect after Revit restarts.
A package declares either commands or an application entry point.

## Assembly selection

The first component of `entry.assembly` is a declared Revit year.
For the running year, the loader replaces that component and keeps the remaining relative path.
`2026/HelloPlugin.dll` selects `2025/HelloPlugin.dll` in Revit 2025 and `2026/HelloPlugin.dll` in Revit 2026.
`2026/lib/Plugin.dll` selects `2025/lib/Plugin.dll` in Revit 2025.
The requested year must appear in `revit` and its DLL must exist in the ZIP.
There is no fallback to a different Revit year.
Dependencies ship beside the main assembly; Revit API assemblies are provided by Revit.

## Icons and ribbon

Icons are flat PNG files at 32x32 pixels.
An optional sibling named `<name>@16.png` supplies the 16x16 image.
The ribbon uses these files for `LargeImage` and `Image`; without the small sibling Revit scales the larger image.
The publisher copies the package icon bytes to a release asset and references that asset in the catalog feed.
Installed catalog rows prefer their installed icon, matching the ribbon.
Unreadable icons fall back to the loader's existing icon treatment.
SVG sources in the samples are editable artwork and are not required in the ZIP.

An empty installation displays only **Add-Ins > DevLoader > Dev**.
Installing a command package creates one button per command.
Updates refresh the labels, tooltips, icons and slot bindings and hide commands removed from the manifest.
Uninstall hides and disables the package buttons.
The Revit API has no ribbon-item removal method; hidden items persist until Revit exits and reinstall reuses them.
The host has 20 command slots shared across packages; a multi-command package consumes one slot per command.
Exceeding the limit fails installation before writing the registry.
Loaded assemblies and static state persist until Revit exits.

Movement: no signature, instant state changes, standard WPF hover, pressed and keyboard focus states.
There are no animations, animation dependencies or animation code (0 KB).
Reduced-motion behavior is identical; this desktop Revit surface has no mobile layout.

## Build and publish

From the repository root on Windows:

```powershell
.\tools\feed\Build-Package.ps1 -Project .\samples\HelloPlugin
.\tools\feed\Build-Package.ps1 -Project .\samples\ElementCounter
.\tools\feed\Build-Package.ps1 -Project .\samples\LevelLister
.\tools\feed\Build-Package.ps1 -Project .\samples\HelloPlugin -Version 1.1.0
Get-ChildItem .\artifacts\packages\*.zip
```

The builder compiles every year in `plugin.json` using `Release.R25` and `Release.R26` configurations and the shared `samples/Directory.Build.props`.
It stamps the requested package and assembly version without changing the source template.
Samples target Revit 2025 and 2026 on `net8.0-windows` through the same Nice3point SDK as the hosts.
The ZIP includes runtime dependencies, per-year output and PNGs; PDB files are omitted.
The builder refuses to overwrite an existing version.
Use `-OutputDir` for a separate package directory.
See [feed publishing](feed-format.md#package-and-publish) for registry generation.

## Compatibility

Existing key-value packages remain readable: `release-info.properties` plus `payload/<year>/` assemblies and an optional root `icon.png`.
Older tooling called these DevPayload packages and used schema 1 or 2 in their properties.
That historical payload schema number is separate from `plugin.json` schema 2.
`plugin.json` takes precedence when both formats exist in a ZIP.
Local discovery scans `*.zip` and ignores unreadable packages.

Installed `.devmanifest` files remain key-value files with machine-specific assembly paths.
Older entries load as a single command with ID `default`.
The registry retains its original keys and stores command metadata and descriptions as base64-encoded UTF-8 JSON/text in `commandsBase64` and `descriptionBase64`.
`iconPath` is relative to the installed run root.
These files are local state and are not portable package manifests.
New packaging tools emit `plugin.json` v2.

## Ideas borrowed from pyRevit

pyRevit organizes extensions as `.extension`, `.tab`, `.panel` and `.pushbutton` bundle directories.
A pushbutton's `bundle.yaml` supplies presentation metadata such as title and tooltip, and `icon.png` supplies its image.
Its extension manager discovers installable repositories through `extensions/extensions.json`.
See the [pyRevit architecture](https://docs.pyrevitlabs.io/architecture/), [bundle definitions](https://docs.pyrevitlabs.io/reference/pyrevit/extensions/) and [extension registry instructions](https://docs.pyrevitlabs.io/custom_extension/).
The [About pushbutton bundle](https://github.com/pyrevitlabs/pyRevit/tree/develop/extensions/pyRevitCore.extension/pyRevit.tab/pyRevit.panel/About.pushbutton) and [extensions.json](https://github.com/pyrevitlabs/pyRevit/blob/develop/extensions/extensions.json) are concrete references.

DevLoader borrows self-describing bundles, adjacent icon files, metadata-driven ribbon construction and registry-based installation.
It uses one JSON manifest for C# commands, per-year compiled assemblies and versioned ZIP assets per channel.
The host owns the DevLoader panel under Add-Ins; packages cannot create arbitrary tabs or panels.
It does not interpret pyRevit YAML, install Git extension repositories or copy pyRevit UI source.
