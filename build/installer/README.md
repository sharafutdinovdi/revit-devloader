# Install Revit DevLoader

Close Revit before installing or removing the loader.
Releases provide unsigned Inno Setup installers and a ZIP with PowerShell scripts.
`SHA256SUMS.txt` contains the SHA256 hashes of the ZIP and both installers.

## Setup executables

| Asset | Scope | Add-in directory |
| --- | --- | --- |
| `revit-devloader-<version>-user-setup.exe` | Current user, no elevation | `%APPDATA%\Autodesk\Revit\Addins\<year>\RevitDevLoader` |
| `revit-devloader-<version>-admin-setup.exe` | All users, requires administrator rights | `%ProgramData%\Autodesk\Revit\Addins\<year>\RevitDevLoader` |

The `.addin` manifest is placed beside the add-in directory and references the DLL with a relative path.
Each packaged Revit year is selectable.
A year is selected by default when `C:\Program Files\Autodesk\Revit <year>\Revit.exe` exists.
The user and admin installers have separate uninstall registrations.
The user uninstaller and LICENSE reside in `%LOCALAPPDATA%\RevitDevLoader\installer`; the admin copies reside in `%ProgramFiles%\RevitDevLoader`.

Silent installation uses the detected years unless `/COMPONENTS` specifies a selection:

```powershell
$process = Start-Process .\revit-devloader-0.3.0-user-setup.exe -Wait -PassThru -ArgumentList '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /COMPONENTS="revit2026" /LOG="install.log"'
$process.ExitCode
```

A comma-separated list selects multiple components, for example `/COMPONENTS="revit2025,revit2026"`.
When Revit is running, silent setup exits with a non-zero code (1 in Inno Setup 6) and writes the reason to its log.
Interactive setup waits for Revit to close before continuing.
On a machine without detected Revit installations, the default selection is empty; select a year explicitly to install its add-in.

Remove the selected scope through Windows Installed apps or its registered uninstaller.
The uninstaller accepts `/VERYSILENT /SUPPRESSMSGBOXES /NORESTART`.
Removal deletes the installed add-in directories and manifests.
Settings at `%LOCALAPPDATA%\RevitDevLoader\settings.properties`, plugin registrations, updates and run folders are preserved.

## ZIP and PowerShell scripts

The ZIP is the scriptable alternative and contains one directory per built Revit year.
Extract it and run:

```powershell
.\install.ps1 -RevitVersion 2026
```

Use `install-all.cmd` or `install.ps1 -AllVersions` to install every year in the package.
The script defaults to the highest packaged year and the current-user add-in directory listed above.

Start Revit and open **Add-Ins > DevLoader > Dev**.
Set the update source in **Settings**.
A repository requires an explicit release tag.
The script can also write the source:

```powershell
.\install.ps1 -RevitVersion 2026 -FeedRepo OWNER/REPO -FeedTag preview
```

Existing feed settings are preserved unless `-ForceSettings` is supplied.
There is no default feed.
Local packages are discovered under `%LOCALAPPDATA%\RevitDevLoader\updates` when the feed is absent or fails.

Remove a script installation with:

```powershell
.\uninstall.ps1 -RevitVersion 2026
```

Use `uninstall-all.cmd` to remove all installed loader versions.
Script removal also preserves settings, payloads and the plugin registry.

## Build and WinGet

Run `build/installer/package.ps1 -SkipBuild` after the release build, then `build/installer/build-installers.ps1 -Version <version> -InstallIfMissing` on Windows.
The installer builder discovers staged years and writes both setup executables to `build/release/packages`.
`INNOSETUP_ISCC` overrides compiler discovery.
The version defaults to `RELEASE_VERSION`, then `0.0.0-local`.

The WinGet workflow generates `Sharafutdinov.RevitDevLoader` manifests from published installer assets and uploads them as an artifact.
Submission requires the maintainer's `WINGET_TOKEN` classic PAT with `public_repo` scope and skips prereleases.
`New-WingetManifests.ps1 -SkipDownload` generates local template checks with dummy hashes; these files must not be submitted.
