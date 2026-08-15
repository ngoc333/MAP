# Module API

MAP.M.Template là bộ ví dụ chạy được dành cho developer module. Mỗi page minh họa cách sử dụng API trong tình huống thực tế.

MAP.M.Template is a runnable example catalog for module developers. Each page demonstrates how to use the APIs in practical scenarios.

Module pages inherit `BasePage`:

```razor
@inherits BasePage
```

## Recommended first

Use these helpers for most module pages:

- `Lang.T("TemplateHome.Title")` translates module text.
- `OpenPageAsync("template-detail")` opens a page and reports navigation failures safely.
- `GetParameter<T>("CustomerId")` returns a value or its default; `RequireParameter<T>("Status")` requires one.
- `NotifySuccess`, `NotifyWarning`, and `NotifyError` display standard notifications.
- `ConfirmAsync("Continue?")` returns `true` only after confirmation.
- `QueryAsync<T>(commandName, parameters)` calls the configured PostgreSQL function.
- `ExecuteAsync(commandName, parameters)` calls the configured PostgreSQL procedure.
- `PageCancellationToken` is cancelled when navigation leaves the page.
- Override `DisposePageAsync()` to clean up page timers, streams, or subscriptions.

Override `HeaderTitleKey`, `HeaderContent`, or `ShowBack`, then call `RefreshHeader()` after changing header state.

## Advanced when needed

`Navigator`, `DbClient`, `ClientContext`, `MenuService`, `UiState`, `ConfigService`, and `LogStore` are available from `BasePage` or injected services. Prefer the helpers above for normal module code.

Use `Navigator.OpenRootAsync(...)` when a page must become the root; it clears Back history. Low-level `DbClient` calls need an explicit database name and should receive `PageCancellationToken`.

See [TEMPLATE-COVERAGE.md](TEMPLATE-COVERAGE.md) for the runnable example index.

## Reference restrictions

Module projects may reference directly only `MAP.C.Contract` and `MAP.C.UI`. Do not reference `MAP.C.Runtime`, `MAP.C.Wpf`, or `MAP.C.Wasm`; `Sdk/MAP.ModuleSdk.props` enforces this boundary.
