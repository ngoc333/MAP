# MAP.M.Template member coverage

Each row identifies the executable lab or the explicit host boundary. `Covered` actions are interactive; destructive/environment-dependent calls are never made during page initialization.

| API member | Tier | Demo page | Mode | Desktop | Web | Status | Notes |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `BasePage.Navigator` | B | Navigation | Live | Yes | Yes | Covered | Direct scenario and back state. |
| `BasePage.Header` | B | Header/UI | Live | Yes | Yes | Covered | Active, Set, Clear and Changed. |
| `BasePage.Lang` | A | Localization | Live | Yes | Yes | Covered | Translation and language switch. |
| `BasePage.DbClient` | B | Database | Environment dependent | Yes | Yes | Covered | Low-level and typed calls. |
| `BasePage.ClientContext` | B | Localization | Live | Yes | Yes | Covered | Context values displayed. |
| `BasePage.MenuService` | B | Localization | Live | Yes | Yes | Covered | Menu tree and loading. |
| `BasePage.Dialogs` | B | Header/UI | Live | Yes | Yes | Covered | Direct alert. |
| `BasePage.Notifications` | B | Header/UI | Live | Yes | Yes | Covered | Direct notification. |
| `BasePage.ErrorNotifier` | B | Logging/error | Live | Yes | Yes | Covered | Correlated notification. |
| `BasePage.PageId`, `PageParameters` | A | Home, Detail | Live | Yes | Yes | Covered | Captured instance values. |
| `BasePage.PageCancellationToken` | A | Database, Lifecycle | Live | Yes | Yes | Covered | DB and timer cancellation. |
| `BasePage.DbName`, `UserName`, `IpAddress` | A | Home, Database | Live | Yes | Yes | Covered | Configured DB is read-only. |
| `BasePage.GetParameter<T>()`, `RequireParameter<T>()` | A | Detail | Live | Yes | Yes | Covered | Conversion and caught required failure. |
| `BasePage.NotifySuccess()`, `NotifyWarning()`, `NotifyError()` | A | Home, Detail | Live | Yes | Yes | Covered | Interactive notifications. |
| `BasePage.ConfirmAsync()` | A | Home, Detail, Config | Live | Yes | Yes | Covered | Confirmation gates effects. |
| `BasePage.QueryAsync<T>()`, `ExecuteAsync()` | A | Database | Environment dependent | Yes | Yes | Covered | Uses page token/default DB. |
| `BasePage.HeaderTitleKey`, `HeaderKind`, `HeaderContent`, `ShowBack`, `RefreshHeader()` | A | Header/UI | Live | Yes | Yes | Covered | Dynamic state; Kind is host gap. |
| `BasePage.OpenPageAsync()` | A | Home, Navigation | Live | Yes | Yes | Covered | Unknown-page failure is contained. |
| `BasePage.DisposePageAsync()` | A | Lifecycle | Live | Yes | Yes | Covered | Cleanup and isolated opt-in failure. |
| `IPageNavigator.Current`, `CanBack`, `Navigating`, `Changed`, `OpenAsync`, `OpenRootAsync`, `BackAsync` | B | Navigation | Live | Yes | Yes | Covered | Cross-instance capped probe history. |
| `IUiStateService.ShowMenu`, `ShowHeader`, `Changed`, `ToggleMenu`, `ToggleHeader`, `SetMenu`, `SetHeader` | B | Header/UI | Live | Yes | Yes | Covered | Every setter has explicit action; state restored. |
| `IDbApiClient.CallOracleAsync`, `CallPostgreSqlFunctionAsync`, `CallPostgreSqlProcedureAsync` | B | Database | Environment dependent | Yes | Yes | Covered | Manual, caught calls only. |
| `QueryPostgreSqlFunctionAsync<T>`, `ExecutePostgreSqlProcedureAsync` | B | Database | Environment dependent | Yes | Yes | Covered | Direct typed extension actions. |
| `IClientContextService.Current`, `ClientContext` properties | B/C | Localization | Live | Yes | Yes | Covered | Program/user/IP/path displayed. |
| `IMenuService.Menus`, `StartPageId`, `DbName`, `OnMenusLoaded`, `LoadMenusAsync`, `FindById` | B | Localization | Live | Yes | Yes | Covered | Cached event behavior explained. |
| `ILanguageService.CurrentLanguage`, `AvailableLanguages`, `LanguageChanged`, `T`, `SetLanguage`, `LoadModuleResourcesAsync` | B | Localization | Live | Yes | Yes | Covered | Event counter and in-memory merge. |
| `IResourceLoader.LoadJsonAsync`, `LoadModuleResourcesAsync` | B | Localization | Live | Yes | Yes | Covered | Embedded and module resource loads. |
| `IAppConfigService.Exists`, `Current`, `GetSystemInfo`, `GetDisplays`, `SaveAsync`, `RestartApp` | B | Config/platform | Guarded side effect | Yes | Yes | Covered | Null-current draft; restart double confirmation. |
| `IPlatformCapabilities.SupportsFullscreen`, `SupportsHideTaskbar`, `SupportsDisplaySelection` | B | Config/platform | Live | Yes | Yes | Covered | Unsupported controls are disabled. |
| `ILogStore.WriteAsync`, `GetDaysAsync`, `GetAsync`, `ClearAsync`; `LogEntry`; `AppSession.Id` | B/C | Logging/error | Guarded side effect | Yes | Yes | Covered | Dedicated 2099-12-31 sandbox only. |
| `IModuleLoader.OnLoadingChanged`, `LoadComponentAsync` | B | Localization | Environment dependent | Yes | Yes | Covered | Cache-hit behavior noted. |
| `PageParams.From`, indexer, dynamic members; `ActivePage`; `PageHistoryEntry` | C | Navigation, Utilities | Synthetic | Yes | Yes | Covered | Success and reflection-failure branches. |
| `MenuTitle.Get`; `MenuTree.Find`, `FindFirstPage`, `ResolveStartupPage`; `MenuConfigValidator.Validate` | C | Utilities | Synthetic | Yes | Yes | Covered | Valid and invalid deterministic cases. |
| `DbJson.Options`, `DbJson.ToElement` | C | Database, Utilities | Synthetic | Yes | Yes | Covered | Snake-case serialization. |
| `ModuleErrorId.Create`, `GetOrCreate`, `Set` | C | Logging/error, Utilities | Synthetic | Yes | Yes | Covered | Stable same-exception ID. |
| `IPageHeaderState.Active`, `Changed`, `Set`, `Clear`; `PageHeader`; `PageHeaderState` | B/C | Header/UI, Utilities | Live/Synthetic | Yes | Yes | Covered | Direct and local state examples. |
| `PageHeaderResolver.GetMatchingHeader`, `ResolveTitle`; `HeaderKind` | C | Utilities, Header/UI | Synthetic/Host gap | Yes | Yes | Host gap | Kind has no visual host rendering. |
| `ModuleErrorNotifier.Notify`; `ModuleErrorBoundary.OnFaulted` | B | Logging/error | Live | Yes | Yes | Covered | Nested opt-in fault boundary. |
| `RadzenLocalizer.Get`; direct `DialogService`, `NotificationService` | B | Localization, Header/UI | Live | Yes | Yes | Covered | Advanced-only examples. |
| `MainLayout`, `AppHeader`, `HeaderClock`, `AppMenu`, `AppMenuItem`, `LanguageSelector`, `PageContainer` | D | Documentation | Host-only / excluded | Yes | Yes | Host-only / excluded | Host composition; do not use as module building blocks. |

There are no `Uncovered` rows. `HeaderKind` remains an explicit host rendering gap; composition components remain intentionally excluded.
