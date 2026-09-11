# Contributing

Report a reproducible problem with the Revit year and loader configuration.
Remove credentials and private paths from logs before sharing them.

## Development

Use Windows and the SDK selected by [global.json](global.json).
The Revit API assemblies are restored from NuGet.
Build both add-in families and run the core tests before submitting a change:

```powershell
dotnet build src/RevitDevLoader.Addin.Legacy -c Release
dotnet build src/RevitDevLoader.Addin.Modern -c Release
dotnet test tests/RevitDevLoader.Core.Tests
```

Use `build/build-all.ps1` for the complete supported year matrix.
Keep shared add-in code under `src/RevitDevLoader.Addin.Shared`.
Keep feed and registry contract changes covered by tests.
Describe the user-visible behavior and validation in the pull request.

## Documentation

Use sentence-case headings and short sentences.
Use real screenshots with private data removed.
Keep build artifacts and credentials outside version control.
