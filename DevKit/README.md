# MAP DevKit V1

DevKit is self-contained: it contains binary MAP SDK dependencies, hosts, shared assets, and one starter module.

## Start

1. Open `DevKit.slnx`.
2. Run `./scripts/build.ps1` in PowerShell.
3. Set `MAP.H.Desktop` or `MAP.H.Web` as startup project and run it.
4. Copy `Modules/MAP.M.Template` to create a module, then rename its project, namespace, localization resource names, and menu entries in `Shared/Config/page.json`.

Module projects may directly reference only `MAP.C.Contract` and `MAP.C.UI`, through `Sdk/MAP.ModuleSdk.props`. Do not reference Runtime, Wasm, or Wpf binaries.

Desktop deployment: `./scripts/deploy-desktop.ps1 -DestinationModulesPath <server-modules-path>`. It compares SHA256 and reports destination timestamp. Web deployment is intentionally not implemented.

`Shared/Config/db-api.json` is a development configuration; provide appropriate endpoints for the target environment without placing credentials in this repository.
