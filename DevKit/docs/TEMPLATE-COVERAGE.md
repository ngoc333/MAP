# MAP.M.Template runnable example index

`MAP.M.Template` is a practical developer demo. Open a lab, interact with an API, and copy its small Razor example into a module.

| Feature | Description | Demo page | API |
| --- | --- | --- | --- |
| Notifications / confirmation | Give standard feedback and ask for approval before an action. | Home | `NotifySuccess`, `NotifyWarning`, `NotifyError`, `ConfirmAsync` |
| Navigation | Open pages, pass parameters, and use Back history. | Navigation | `OpenPageAsync`, `Navigator.BackAsync`, `Navigator.OpenRootAsync` |
| Parameters | Read optional and required page values. | Detail | `GetParameter`, `RequireParameter` |
| Header | Change the page header and Back button. | Header & UI | `HeaderTitleKey`, `HeaderContent`, `ShowBack`, `RefreshHeader` |
| UI visibility | Show or hide the host menu and header. | Header & UI | `IUiStateService` |
| Localization | Read translated text and change the active language. | Localization, context & menu | `Lang.T`, `Lang.SetLanguage` |
| Client context | Inspect current host environment values. | Localization, context & menu | `ClientContext` |
| Menu | Find menu items and load their components. | Localization, context & menu | `Menus`, `FindById`, `ModuleLoader.LoadComponentAsync` |
| PostgreSQL query | Query PostgreSQL and map rows to DTOs. | Database | `QueryAsync<T>` |
| PostgreSQL procedure | Run a procedure when no result is needed. | Database | `ExecuteAsync` |
| Low-level database | Use an explicit database and JSON response. | Database | `IDbApiClient` PostgreSQL methods |
| Oracle | Send an Oracle API request with JSON. | Database | `CallOracleAsync` |
| Configuration | Read and save host configuration after confirmation. | Config & platform | `IAppConfigService` |
| Platform capabilities | Inspect fullscreen, taskbar, and display support. | Config & platform | `IPlatformCapabilities` |
| Logging | Write, read, and clear sandbox logs. | Logging & errors | `ILogStore`, `LogEntry` |
| Error correlation | Connect module errors to a short ID. | Logging & errors | `ModuleErrorId`, `ErrorNotifier` |
| Cancellation / cleanup | Stop page-lifetime work and release resources. | Lifecycle | `PageCancellationToken`, `DisposePageAsync` |
| Utilities | Build page parameters, JSON, and menu-tree lookups. | Utilities | `PageParams`, `DbJson`, `MenuTree`, `MenuTitle` |

Database actions are manual and safely display backend errors. Logging only writes and clears the `2099-12-31` sandbox day. Configuration save and restart are separate, confirmed actions.
