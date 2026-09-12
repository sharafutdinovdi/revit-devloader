# Security policy

Private vulnerability reporting is enabled for this repository.
Report vulnerabilities through [Report a vulnerability](https://github.com/sharafutdinovdi/revit-devloader/security/advisories/new).
Email [sharafutdinov.di.dev@outlook.com](mailto:sharafutdinov.di.dev@outlook.com) if the private reporting form is unavailable.
Include the affected version and steps to reproduce.
Do not include credentials or confidential model data.
Do not disclose vulnerabilities in public issues or Discussions before coordinated disclosure.

## Trust boundary

Payloads execute inside Revit with the current user's permissions.
Use trusted feeds and packages.
A matching SHA-256 confirms the advertised archive bytes and does not establish publisher identity.
Settings and registry files rely on operating system permissions.
Logs can contain local paths and feed locations.
See [path checks](docs/how-it-works.md#path-checks) for the extraction boundary.
