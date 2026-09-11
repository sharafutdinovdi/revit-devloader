# Install Revit DevLoader

Close Revit before installing or removing the loader.
The package contains one directory per built Revit year.

```powershell
.\install.ps1 -RevitVersion 2026
```

Use `install-all.cmd` or `install.ps1 -AllVersions` to install every year in the package.
The default installs the highest packaged year.
Files are copied to `%APPDATA%\Autodesk\Revit\Addins\<year>\RevitDevLoader`.
The `.addin` manifest is placed beside that directory.

Start Revit and open **Add-Ins > DevLoader > Dev**.
Set the update source in **Settings**.
A repository requires an explicit release tag.
The installer can also write the source:

```powershell
.\install.ps1 -RevitVersion 2026 -FeedRepo OWNER/REPO -FeedTag preview
```

Settings are stored at `%LOCALAPPDATA%\RevitDevLoader\settings.properties`.
Existing feed settings are preserved unless `-ForceSettings` is supplied.
There is no default feed.
Local packages are discovered under `%LOCALAPPDATA%\RevitDevLoader\updates` when the feed is absent or fails.

Remove the loader with:

```powershell
.\uninstall.ps1 -RevitVersion 2026
```

Use `uninstall-all.cmd` to remove all installed loader versions.
Uninstalling the loader preserves settings, payloads and the plugin registry.
