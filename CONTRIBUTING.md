# Contributing

Report a reproducible problem with the Revit year and loader configuration.
Remove credentials and private paths from logs before sharing them.

## Development

Use Windows and the SDK selected by [global.json](global.json).
The Revit API assemblies are restored from NuGet.
Build both add-in families and run the core tests before submitting a change:

```powershell
.\build\build-all.ps1 -SkipTests
dotnet test tests/RevitDevLoader.Core.Tests -c Release
.\build\installer\package.ps1 -SkipBuild
```

The build script produces `build/release/<year>` for Revit 2022-2026.
Tests run after the build to include the release manifest layout check.
For a faster default-host check, run `dotnet build src/RevitDevLoader.Addin.Legacy -c Release` and `dotnet build src/RevitDevLoader.Addin.Modern -c Release`.
Those configurations target Revit 2023 and 2026.
Installer ZIPs appear under `build/release/packages`.
Keep `REVITDEVLOADER_FEED_CHECK` unset for ordinary tests; the opt-in feed check installs real payloads.
Keep shared add-in code under `src/RevitDevLoader.Addin.Shared`.
Keep feed and registry contract changes covered by tests.
Describe the user-visible behavior and validation in the pull request.

## Documentation

Use sentence-case headings and short sentences.
Use real screenshots with private data removed.
Keep build artifacts and credentials outside version control.
