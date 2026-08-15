# MAP.M.Template runnable example index

`MAP.M.Template` is a practical developer demo. Open a lab, interact with an API, and copy its small Razor example into a module.

| Feature | Demo page | Example |
| --- | --- | --- |
| Notifications / confirmation | Home | `NotifySuccess`, `NotifyWarning`, `NotifyError`, `ConfirmAsync` |
| Navigation | Navigation | `OpenPageAsync`, `Navigator.BackAsync`, `Navigator.OpenRootAsync` |
| Parameters | Detail | `GetParameter`, `RequireParameter` |
| Header | Header & UI | `HeaderTitleKey`, `HeaderContent`, `ShowBack`, `RefreshHeader` |
| UI visibility | Header & UI | `IUiStateService` |
| Localization | Localization, context & menu | `Lang.T`, `Lang.SetLanguage` |
| Client context | Localization, context & menu | `ClientContext` |
| Menu | Localization, context & menu | `Menus`, `FindById` |
| Module loading | Localization, context & menu | `ModuleLoader.LoadComponentAsync` |
| PostgreSQL query | Database | `QueryAsync<T>` |
| PostgreSQL procedure | Database | `ExecuteAsync` |
| Low-level database | Database | `IDbApiClient` PostgreSQL methods |
| Oracle | Database | `CallOracleAsync` |
| Configuration | Config & platform | `IAppConfigService` |
| Platform capabilities | Config & platform | `IPlatformCapabilities` |
| Logging | Logging & errors | `ILogStore`, `LogEntry` |
| Error correlation | Logging & errors | `ModuleErrorId`, `ErrorNotifier` |
| Cancellation / cleanup | Lifecycle | `PageCancellationToken`, `DisposePageAsync` |
| Utilities | Utilities | `PageParams`, `DbJson`, `MenuTree`, `MenuTitle` |

Database actions are manual and safely display backend errors. Logging only writes and clears the `2099-12-31` sandbox day. Configuration save and restart are separate, confirmed actions.
