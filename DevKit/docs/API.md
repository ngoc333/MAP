# Module API

Pages inherit `BasePage`:

```razor
@inherits BasePage
```

Use `BasePage` helpers first:

- `Lang.T(key)` translates a key from module `Localization/vi.json` and `Localization/en.json`. Use one JSON section level, for example `"TemplateHome": { "Title": "..." }` with `Lang.T("TemplateHome.Title")`.
- `OpenPageAsync(pageId, new { CustomerId = 42 })` navigates safely, adds the current page to Back history by default, and reports navigation failures through the module error notifier.
- `OpenPageAsync("final", pushHistory: false)` opens a page without adding the current page to Back history; it does not remove existing history. Use `Navigator.BackAsync()` to return according to the LIFO history stack.
- `GetParameter<T>(name)` returns default when the parameter is absent or cannot be converted; `RequireParameter<T>(name)` throws in those cases.
- `NotifySuccess`, `NotifyWarning`, and `NotifyError` use the existing Radzen notification service.
- `ConfirmAsync(message)` returns `true` only when confirmed.
- `QueryAsync<T>(commandName, parameters)` invokes the configured PostgreSQL function and automatically uses the page cancellation token.
- `ExecuteAsync(commandName, parameters)` invokes the configured PostgreSQL procedure and automatically uses the page cancellation token.
- `DbClient`, `DbName`, `UserName`, `IpAddress`, `PageId`, and `PageParameters` are available for advanced scenarios. `PageId` and `PageParameters` belong to the current page instance; they do not change when navigation moves to another page.
- `PageCancellationToken` is cancelled as soon as navigation starts away from the page. Use it for non-database page-lifetime work such as HTTP calls, delays, loops, and APIs that accept a cancellation token.
- Navigation parameters can remain in Back history until popped or root navigation clears history. Pass IDs or small value/DTO state only; do not pass large collections, buffers, streams, connections, services, or component instances.
- Do not implement disposal interfaces in a module page. Override the single `DisposePageAsync()` hook for synchronous cleanup (timers and event subscriptions) and asynchronous cleanup (for example JS modules or streams). The framework logs and isolates cleanup failures so they do not fault the renderer.
- Override `HeaderTitleKey`, `HeaderKind`, `HeaderContent`, or `ShowBack`, then call `RefreshHeader()` after changing header state.

`Navigator`, `Dialogs`, `Notifications`, `ClientContext`, `MenuService`, and `ErrorNotifier` are protected injected services for advanced scenarios. Prefer `OpenPageAsync` and notification helpers for normal page behavior.

## Reference restrictions

Module projects may reference directly only:

- `MAP.C.Contract`
- `MAP.C.UI`

Do not reference `MAP.C.Runtime`, `MAP.C.Wpf`, or `MAP.C.Wasm`. The provided `Sdk/MAP.ModuleSdk.props` enforces the supported direct SDK references.

Additional NuGet or library dependencies are allowed. Desktop deployment stages and deploys their private runtime DLLs automatically, excluding host-owned MAP platform DLLs and framework DLLs.

V1 uses Radzen components and services directly. `IUiStateService` and `IPageNavigator` are advanced APIs; prefer `BasePage` helpers first.

## Interactive template catalog

`MAP.M.Template` provides capability-oriented smoke-test labs for navigation, headers/UI state, localization/context/menu, database, configuration/platform, logging/errors, lifecycle, and pure utilities. See [TEMPLATE-COVERAGE.md](TEMPLATE-COVERAGE.md) for the supported API matrix, host-only exclusions, and known host gaps such as `HeaderKind` not currently being rendered.
