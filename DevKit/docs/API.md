# Module API

Pages inherit `BasePage`:

```razor
@inherits BasePage
```

- `Lang.T(key)` translates a key from module `Localization/vi.json` and `Localization/en.json`. Use one JSON section level, for example `"TemplateHome": { "Title": "..." }` with `Lang.T("TemplateHome.Title")`.
- `OpenPageAsync(pageId, new { CustomerId = 42 })` navigates safely.
- `GetParameter<T>(name)` returns default when absent; `RequireParameter<T>(name)` throws a clear error.
- `NotifySuccess`, `NotifyWarning`, and `NotifyError` use the existing Radzen notification service.
- `ConfirmAsync(message)` returns `true` only when confirmed.
- `QueryAsync<T>(commandName, parameters)` invokes the configured PostgreSQL function.
- `ExecuteAsync(commandName, parameters)` invokes the configured PostgreSQL procedure.
- `DbClient`, `DbName`, `UserName`, `IpAddress`, `PageParameters`, and `FromPageId` are available when needed.

V1 uses Radzen components and services directly. `IUiStateService` and `IPageNavigator` are advanced APIs; prefer `BasePage` helpers first.
