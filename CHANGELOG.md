# Changelog

## 0.2.0 - 2026-09-12

- Per-add-in icons from the feed (`icon` asset) or `icon.png` in the package, with a coloured letter fallback.
- Update available state with an Update action when the feed carries a newer version for the running Revit year.
- Uninstall action: removes the registration, hides and disables the ribbon button, keeps the run folder.
- Open plugin folder and Show versions actions per installed row.
- Inline install, update and uninstall results in the row instead of a confirmation dialog.
- Search box filters the catalog.
- Command payloads register their ribbon button immediately after install.
- Contributor governance: issue and pull request templates, CODEOWNERS, Dependabot, labeler, welcome and stale bots, CodeQL, PR checks with build artifacts.

## 0.1.0 - 2026-09-11

- Generic feed and local package discovery.
- Separate run directories, package verification and a persistent plugin registry.
- Legacy and Modern add-in projects for Revit 2022-2026.
- PowerShell package, feed and installer tools.
- Command ribbon slots and application manifests for loading on the next Revit start.
- Run retention, settings editor and rolling file logs.
- Windows CI for all configured Revit years, core tests and downloadable Legacy and Modern artifacts.
- Tag-triggered installer ZIP publishing to GitHub Releases.
- Feed, architecture, contribution and dependency license documentation.
