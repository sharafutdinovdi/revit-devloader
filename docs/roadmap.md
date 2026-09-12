# Roadmap

Review date: 2026-09-11.

## Known gaps

- Ribbon buttons are pre-created slots (20) that are rebound at runtime, because the Revit API only creates ribbon items during startup.
- Package integrity relies on SHA-256 from the feed over TLS; signed feeds and packages are not implemented.
- Application packages (IExternalApplication) take effect on the next Revit start.
- A `dotnet new` template for the sample plugin layout is not published yet.
- Validate live command execution and application installation in each configured Revit year; the current recording covers application installation in Revit 2026.
- Add assembly unloading and dependency isolation; loaded assemblies and plugin static state persist until Revit exits.
- Implement bootstrap self-update; installing the loader as an application payload can create a second loaded copy.
- Replace blocking HTTP calls with cancellable asynchronous I/O and drain both GitHub CLI output streams before enforcing a process timeout.
- Harden GitHub URI segments, Windows process argument quoting and public registry paths against malformed inputs; feeds and local metadata currently require trust.
- Make installation and cache replacement transactional; failures can leave partial runs or temporary files.
- Protect every registered Revit year's active run during retention; cleanup currently protects only the newest installation's run.
- Report skipped registry, conventional add-in and local package reads through a shared diagnostic channel; several best-effort probes still suppress exceptions.
- Rename the public `ShadowCopyCommandRunner` in a documented API change; it loads existing run paths and performs no shadow copy.
- Define a registry schema migration before aligning the persisted `pluginName` key and public `PluginName` properties with feed `pluginId`.
- Validate installer settings before copying binaries; invalid feed arguments can fail after the loader files have been installed.
- Reject malformed feed entries individually and require deterministic release timestamps; invalid dates currently become the current UTC time.
- Pin Revit API package versions or introduce a lock-file policy; year wildcards can resolve to newer reference packages.
- Add the canon hero and a framed test-run screenshot from real captures; existing catalog screenshots and the GIF remain the available visuals.
