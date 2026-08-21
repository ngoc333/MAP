# Module API

MAP.M.Template là bộ ví dụ chạy được dành cho developer module. Mỗi page minh họa cách sử dụng API trong tình huống thực tế.

MAP.M.Template is a runnable example catalog for module developers. Each page demonstrates how to use the APIs in practical scenarios.

Module pages inherit `BasePage`:

```razor
@inherits BasePage
```

## BasePage core

`BasePage` intentionally contains only capabilities used by nearly every module page:

- `Lang.T("TemplateHome.Title")` translates module text.
- `OpenPageAsync("template-detail")` opens a page and reports navigation failures safely.
- `GetParameter<T>("CustomerId")` returns a converted value or its default.
- `TryGetParameter<T>("CustomerId", out var value)` distinguishes missing/invalid values from valid default values.
- `QueryAsync<T>(commandName, parameters)` maps PostgreSQL function `data` arrays to `List<T>`.
- `QuerySingleAsync<T>(commandName, parameters)` maps PostgreSQL function `data` to a single model.
- `QueryAsync(commandName, parameters)` returns the validated raw JSON API response.
- `ExecuteAsync(commandName, parameters)` executes a PostgreSQL procedure and returns the validated raw JSON API response.
- `DbName` is resolved from the current menu database configuration.
- `PageCancellationToken` is cancelled when navigation leaves the page.
- Override `DisposePageAsync()` to clean up page timers, streams, or subscriptions.

`Navigator`, `Lang`, `DbClient`, and `MenuService` remain available from `BasePage` because navigation, localization, and database access are core MAP page capabilities.

## Optional capabilities

UI and platform services are injected only by pages that need them.

### Client context

```razor
@inject IClientContextService ClientContext
```

Use `ClientContext.Current.UserName`, `ClientContext.Current.IpAddress`, `ProgramId`, or `LocalPath` when required. These values are no longer exposed by every page through `BasePage`.

### Dialogs and notifications

```razor
@inject DialogService Dialogs
@inject NotificationService Notifications
```

Use `Dialogs.Confirm(...)` / `Dialogs.Alert(...)` for dialog scenarios and `Notifications.Notify(...)` for toast notifications. `BasePage` does not carry dialog or notification helpers.

### Custom page header

Normal page titles come from current menu metadata through `MainLayout` and `PageHeaderResolver`; normal pages do not need to configure a header.

Only pages with dynamic/custom header behavior should inject the header state:

```razor
@inject IPageHeaderState Header
```

Then publish an explicit header when needed:

```csharp
Header.Set(new PageHeader(
    PageId,
    HeaderKind.Default,
    "MyPage.CustomTitle",
    content: null,
    showBack: true));
```

### UI state

Inject `IUiStateService` only when a page intentionally changes global menu/header visibility.

## Advanced when needed

Use `Navigator.OpenRootAsync(...)` when a page must become the root; it clears Back history. Low-level `DbClient` calls need an explicit database name and should receive `PageCancellationToken`.

See [TEMPLATE-COVERAGE.md](TEMPLATE-COVERAGE.md) for the runnable example index.

## Reference restrictions

Module projects may reference directly only `MAP.C.Contract` and `MAP.C.UI`. Do not reference `MAP.C.Runtime`, `MAP.C.Wpf`, or `MAP.C.Wasm`; `Sdk/MAP.ModuleSdk.props` enforces this boundary.
