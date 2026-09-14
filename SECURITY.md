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

## Verify downloads

Release assets use GitHub build provenance attestations instead of Authenticode signing.
Run `gh attestation verify revit-devloader-<version>-user-setup.exe --owner sharafutdinovdi` with the downloaded version, or substitute another release asset's filename.
Successful verification binds the file's digest to the workflow and source commit shown in the output; check that they identify this repository's `.github/workflows/release.yml` at the expected commit.
The owner constraint accepts any repository owned by `sharafutdinovdi`.
There is no Authenticode signature, and Windows SmartScreen warnings and Revit's unsigned add-in dialog still appear.
See [Verify downloads](build/installer/README.md#verify-downloads) for details and coverage of older releases.
