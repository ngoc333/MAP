# MAP.M.Template coverage

The template is an interactive API catalog.  `Live` actions are always initiated by the developer; no database, save, restart, log-clear, or fault action runs during initialization.

| API / surface | Tier | Demo page | Mode | Desktop | Web | Notes |
| --- | ---: | --- | --- | --- | --- | --- |
| `BasePage` parameters, notifications, confirm, identity, page state | A | Home, Detail | Live | Yes | Yes | Invalid required parameter is caught in the action. |
| `BasePage.OpenPageAsync`, `QueryAsync`, `ExecuteAsync`, cancellation, header hooks, cleanup | A | Navigation, Database, Header/UI, Lifecycle | Guarded side effect | Yes | Yes | Database remains offline-safe. |
| `IPageNavigator`, `PageParams`, `ActivePage`, `PageHistoryEntry` | B/C | Navigation, Utilities | Live/Synthetic | Yes | Yes | LIFO and no-push semantics shown. |
| `IUiStateService`, `IPageHeaderState`, `PageHeader` | B/C | Header/UI, Utilities | Live/Synthetic | Yes | Yes | Original visibility is restored on disposal. |
| `HeaderKind`, `PageHeaderResolver`, `PageHeaderState` | C | Header/UI, Utilities | Host gap/Synthetic | Yes | Yes | `HeaderKind` is stored but not visually rendered by the current host. |
| `ILanguageService`, `IResourceLoader`, `RadzenLocalizer` | B/C | Localization/context/menu | Live | Yes | Yes | Resource load is button initiated and repeat-safe. |
| `IClientContextService`, `ClientContext`, `IMenuService`, menu models/utilities | B/C | Localization/context/menu, Utilities | Live/Synthetic | Yes | Yes | Menu event may already have completed due to cache. |
| `IModuleLoader` | B | Localization/context/menu | Environment dependent | Yes | Yes | Cache hits may resolve without loading events. |
| `IDbApiClient`, typed extensions, `DbJson` | B/C | Database, Utilities | Environment dependent/Synthetic | Yes | Yes | Raw errors and cancellation are displayed; no fake server response. |
| `IAppConfigService`, `IPlatformCapabilities`, config/display/system models | B/C | Config/platform | Guarded side effect | Yes | Yes | Draft clone; save is confirmed; restart is separately double-confirmed. |
| `ILogStore`, `LogEntry`, `AppSession` | B/C | Logging/error | Guarded side effect | Yes | Yes | Uses only sandbox day `2099-12-31`. |
| `ModuleErrorId`, `ModuleErrorNotifier`, `ModuleErrorBoundary` | B/C | Logging/error | Live/Synthetic | Yes | Yes | Notifier does not log; host boundary is infrastructure-owned. |
| Host layout/components (`MainLayout`, `AppHeader`, menu components, `PageContainer`) | D | Documentation only | Host-only / excluded | Yes | Yes | Host composition, not normal module dependencies. |

All public Contract/UI surfaces usable by a normal module are represented above; host composition is explicitly excluded rather than promoted as a module API.
