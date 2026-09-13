# Changelog

All notable changes to this project are documented here.
The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and versions follow [Semantic Versioning](https://semver.org/spec/v2.0.0.html).
The Release workflow copies the section for the tagged version into the GitHub release notes.

## [Unreleased]

### Added

- GitHub Pages documentation with MkDocs Material, local previews, strict PR builds, `llms.txt` and `llms-full.txt`.

- User and admin Inno Setup installers alongside the release ZIP, with SHA256 checksums.
- Windows CI smoke tests for silent install and uninstall, component selection, preserved settings and the running Revit guard.
- WinGet manifest generation and optional submission for stable releases when `WINGET_TOKEN` is configured.
- `CODE_OF_CONDUCT.md` (Contributor Covenant 2.1), `AGENTS.md` and `CLAUDE.md` for contributors and coding agents.
- Release notes start with a Highlights section taken from this changelog and direct install links.

### Changed

- README leads with who the tool is for and a three-step install from the release; build-from-source and the sample plugin walkthrough moved to `CONTRIBUTING.md`.
- This changelog follows the Keep a Changelog layout.

## [0.3.0] - 2026-09-12

### Added

- Plugin packages with a `plugin.json` manifest, per-year assemblies and PNG icons; v1 key-value packages still load.
- The DevLoader ribbon panel is generated from installed packages: one button per declared command with the package icon.
- Three sample plugins in `samples/` (Hello Plugin, Element Counter, Level Lister) with a shared build and `tools/feed/Build-Package.ps1`.
- Feed registry carries display names, descriptions and icons; `Publish-DevLoaderFeed.ps1` uploads icons with the packages.
- Documentation: `docs/plugin-package.md`, updated feed format and how-it-works.

## [0.2.0] - 2026-09-12

### Added

- Per-add-in icons from the feed (`icon` asset) or `icon.png` in the package, with a coloured letter fallback.
- Update available state with an Update action when the feed carries a newer version for the running Revit year.
- Uninstall action: removes the registration, hides and disables the ribbon button, keeps the run folder.
- Open plugin folder and Show versions actions per installed row.
- Search box filters the catalog.
- Contributor governance: issue and pull request templates, CODEOWNERS, Dependabot, labeler, welcome and stale bots, CodeQL, PR checks with build artifacts.

### Changed

- Inline install, update and uninstall results in the row instead of a confirmation dialog.
- Command payloads register their ribbon button immediately after install.

## [0.1.0] - 2026-09-11

### Added

- Generic feed and local package discovery.

[Unreleased]: https://github.com/sharafutdinovdi/revit-devloader/compare/v0.3.0...HEAD
[0.3.0]: https://github.com/sharafutdinovdi/revit-devloader/compare/v0.2.0...v0.3.0
[0.2.0]: https://github.com/sharafutdinovdi/revit-devloader/compare/v0.1.0...v0.2.0
[0.1.0]: https://github.com/sharafutdinovdi/revit-devloader/releases/tag/v0.1.0
