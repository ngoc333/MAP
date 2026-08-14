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
- `QueryAsync<T>(commandName, parameters)` invokes the configured PostgreSQL function.
- `ExecuteAsync(commandName, parameters)` invokes the configured PostgreSQL procedure.
- `DbClient`, `DbName`, `UserName`, `IpAddress`, and `PageParameters` are available for advanced scenarios.
- Pass `cancellationToken: PageCancellationToken` to page-lifetime database or background operations. The token is cancelled when the page leaves the UI.
- Do not implement `IDisposable` or `IAsyncDisposable` in a module page. Override `DisposePage()` for synchronous cleanup (timers and event subscriptions) and `DisposePageAsync()` for asynchronous cleanup (for example JS modules or streams).
- Override `HeaderTitleKey`, `HeaderKind`, `HeaderContent`, or `ShowBack`, then call `RefreshHeader()` after changing header state.

`Navigator`, `Dialogs`, `Notifications`, `ClientContext`, `MenuService`, and `ErrorNotifier` are protected injected services for advanced scenarios. Prefer `OpenPageAsync` and notification helpers for normal page behavior.

## Reference restrictions

Module projects may reference directly only:

- `MAP.C.Contract`
- `MAP.C.UI`

Do not reference `MAP.C.Runtime`, `MAP.C.Wpf`, or `MAP.C.Wasm`. The provided `Sdk/MAP.ModuleSdk.props` enforces the supported direct SDK references.

Additional NuGet or library dependencies are allowed. Desktop deployment stages and deploys their private runtime DLLs automatically, excluding host-owned MAP platform DLLs and framework DLLs.

V1 uses Radzen components and services directly. `IUiStateService` and `IPageNavigator` are advanced APIs; prefer `BasePage` helpers first.
