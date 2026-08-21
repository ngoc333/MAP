# MAP.M.Template runnable example index

`MAP.M.Template` is a practical developer demo. Open a lab, interact with an API, and copy its small Razor example into a module.

| Feature | Description | Demo page | API |
| --- | --- | --- | --- |
| Notifications / confirmation | Inject feedback services only on pages that need them. | Home | `NotificationService`, `DialogService` |
| Navigation | Open pages, pass parameters, and use Back history. | Navigation | `OpenPageAsync`, `Navigator.BackAsync`, `Navigator.OpenRootAsync` |
| Parameters | Read optional values and explicitly detect missing/invalid values. | Detail | `GetParameter`, `TryGetParameter` |
| Header | Publish a custom page header only when menu metadata is insufficient. | Header & UI | `IPageHeaderState`, `PageHeader` |
| UI visibility | Show or hide the host menu and header. | Header & UI | `IUiStateService` |
| Localization | Read translated text and change the active language. | Localization, context & menu | `Lang.T`, `Lang.SetLanguage` |
| Client context | Inject and inspect current host environment values. | Localization, context & menu | `IClientContextService` |
| Menu | Find menu items and load their components. | Localization, context & menu | `MenuService`, `FindById`, `ModuleLoader.LoadComponentAsync` |
| PostgreSQL typed query | Query PostgreSQL and map array data to DTOs. | Database | `QueryAsync<T>` |
| PostgreSQL single query | Map object data to one DTO. | Database | `QuerySingleAsync<T>` |
| PostgreSQL raw query | Keep the validated JSON response for dynamic data. | Database | `QueryAsync` |
| PostgreSQL procedure | Run a procedure and optionally consume its validated JSON response. | Database | `ExecuteAsync` |
| Low-level database | Use an explicit database and protocol-level JSON response. | Database | `IDbApiClient` PostgreSQL methods |
| Oracle | Send an Oracle API request with JSON. | Database | `CallOracleAsync` |
| Configuration | Read and save host configuration after confirmation. | Config & platform | `IAppConfigService`, `DialogService` |
| Platform capabilities | Inspect fullscreen, taskbar, and display support. | Config & platform | `IPlatformCapabilities` |
| Logging | Write, read, and clear sandbox logs. | Logging & errors | `ILogStore`, `LogEntry` |
| Error correlation | Connect module errors to a short ID. | Logging & errors | `ModuleErrorId`, `ErrorNotifier` |
| Cancellation / cleanup | Stop page-lifetime work and release resources. | Lifecycle | `PageCancellationToken`, `DisposePageAsync` |
| Utilities | Build page parameters, JSON, and menu-tree lookups. | Utilities | `PageParams`, `DbJson`, `MenuTree`, `MenuTitle` |

Database actions are manual and safely display backend errors. Logging only writes and clears the `2099-12-31` sandbox day. Configuration save and restart are separate, confirmed actions.
