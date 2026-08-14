# MAP DevKit V1

DevKit is self-contained: it contains binary MAP SDK dependencies, hosts, shared assets, and a starter module. It can be copied outside the MAP repository and built without accessing MAP source files.

## Start

1. Open `DevKit.slnx`.
2. Run `./scripts/build.ps1` in PowerShell.
3. Set `MAP.H.Desktop` or `MAP.H.Web` as startup project and run it.
4. Copy `Modules/MAP.M.Template` to create a module, then rename its project, namespace, localization resource names, and menu entries in `Shared/Config/page.json`.

`build.ps1` discovers and builds every `*.csproj` below `Modules/`, then builds both hosts. It fails immediately when a `dotnet` command fails.

All host configuration and common static assets have one source in `Shared/`. Both Desktop and Web consume `Shared/Config/page.json`, so update menu configuration only there.

Module projects may directly reference only `MAP.C.Contract` and `MAP.C.UI`, through `Sdk/MAP.ModuleSdk.props`. Do not reference Runtime, Wasm, or Wpf binaries.

## Desktop deployment

Run:

```powershell
./scripts/deploy-desktop.ps1 -DestinationModulesPath <server-modules-path>
```

The script discovers every module, stages each module's runtime dependency closure in `artifacts/modules/<ModuleName>/`, and deploys module-owned DLLs to the destination. Platform SDK DLLs and framework DLLs are excluded. Shared private dependencies with different SHA256 values cause deployment to fail before any copy occurs.

Files are compared and verified with SHA256. New and changed files are copied atomically and their destination UTC timestamp is reported. Existing unrelated files are not removed: stale dependency cleanup is not automatic in V1 because multiple DevKits can share a server modules folder.

Web deployment is intentionally not implemented.

`Shared/Config/db-api.json` is a development configuration; provide appropriate endpoints for the target environment without placing credentials in this repository.
