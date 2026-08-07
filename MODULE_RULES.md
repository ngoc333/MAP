# MODULE RULES

Rules for Module developers in the MAP framework.

---

## Allowed References

Module projects may reference:

```
MAP.C.Contract
MAP.C.UI
Radzen.Blazor
```

These provide:

- Contracts (interfaces, models, configuration)
- UI framework and components
- Blazor UI library

---

## Forbidden References

Module projects must NOT reference:

```
MAP.C.Wpf
MAP.C.Wasm
MAP.H.Desktop
MAP.H.Web
```

These are platform-specific runtime and host projects. Modules must be platform-agnostic.

---

## Async Rules

### Do NOT use `async void`

```csharp
// WRONG - exception will crash the app
async void HandleClick()
{
    await SomeAsync();
}

// CORRECT
async Task HandleClick()
{
    await SomeAsync();
}
```

Exception: API event handlers that require `async void` (e.g., Blazor event callbacks).

### Do NOT discard fire-and-forget tasks

```csharp
// WRONG - exception is silently lost
_ = SomeAsync();

// CORRECT - await the task
await SomeAsync();

// CORRECT - if you must fire-and-forget, use safe wrapper
_ = SafeFireAndForget(SomeAsync());
```

---

## Process Control Rules

Module must NOT call directly:

```csharp
Environment.Exit(...)
Environment.FailFast(...)
Application.Current.Shutdown(...)
Process.Kill(...)
window.location.href = ...
window.location.replace(...)
```

If platform/process behavior is needed, use Core contracts.

---

## Error Handling Rules

### Let Core handle module errors

The framework provides error isolation:

- **Navigation errors**: Caught by `PageNavigator` and `MainLayout.OpenPageSafeAsync`
- **Render/lifecycle errors**: Caught by `ModuleErrorBoundary`

Modules should:

1. Throw exceptions when errors occur (don't swallow them silently)
2. Let the framework's error isolation handle user notification and logging
3. Not try to catch and display errors themselves

### Exception types

Throw meaningful exceptions:

```csharp
// GOOD
throw new InvalidOperationException("Cannot process order: customer ID is missing.");
throw new FileNotFoundException("Configuration file not found.", filePath);

// BAD - don't throw generic exceptions without context
throw new Exception("Error");
```

---

## Localization Rules

Use the `ILanguageService` for user-facing strings:

```csharp
@inject ILanguageService Lang

<p>@Lang.T("myModule.title", "Default Title")</p>
```

Module localization resources are loaded automatically when the module assembly is loaded.

---

## Navigation Rules

### Don't navigate directly

Use `IPageNavigator` through the framework:

```csharp
@inject IPageNavigator Navigator

// Navigate to a page
await Navigator.OpenAsync("page-id");

// Go back
await Navigator.BackAsync();
```

### Use safe navigation from Module UI

Module pages should use the `OpenPageAsync` method inherited from `BasePage`
for navigation triggered by UI events (button clicks, links, etc.).

```csharp
// GOOD — safe navigation, error handled gracefully
await OpenPageAsync("other-page");
await OpenPageAsync("other-page", new { Id = 42 });

// BAD — raw navigation, error propagates to Module's ErrorBoundary
await Navigator.OpenAsync("other-page");
```

`OpenPageAsync` catches navigation exceptions and shows a notification
instead of letting the error crash the current Module.

Raw `Navigator.OpenAsync` is still available for special cases
(e.g., imperative navigation in non-BasePage components) but
Module pages should prefer `OpenPageAsync`.

### Don't create your own navigation UI

The framework provides:

- Header with back button
- Menu with page links

Modules should not create their own navigation controls.

---

## UI Component Rules

### Use Radzen components

The framework uses Radzen Blazor for UI components:

```razor
<RadzenButton Text="Click me" Click="HandleClick" />
<RadzenTextBox @bind-Value="model.Name" />
<RadzenDataGrid Data="@items" />
```

### Don't create competing UI frameworks

Don't add:

- Other Blazor UI libraries (MudBlazor, MatBlazor, etc.)
- Custom CSS frameworks that conflict with Radzen
- JavaScript UI libraries that duplicate Radzen functionality

---

## Configuration Rules

### Read configuration through contracts

```csharp
@inject IAppConfigService ConfigService

var config = ConfigService.Current;
if (config?.ShowMenu == true)
{
    // ...
}
```

### Don't write configuration directly

Configuration changes should go through `IAppConfigService.SaveAsync()`.

---

## Logging Rules

### Use ILogger

```csharp
@inject ILogger<MyComponent> Logger

Logger.LogInformation("Processing order. OrderId={OrderId}", orderId);
Logger.LogError(exception, "Failed to process order. OrderId={OrderId}", orderId);
```

### Don't log sensitive data

```csharp
// WRONG
Logger.LogInformation("User login. Password={Password}", password);
Logger.LogInformation("Database connection. ConnectionString={ConnectionString}", connStr);

// CORRECT
Logger.LogInformation("User login. UserId={UserId}", userId);
Logger.LogInformation("Database connection attempt.");
```

---

## Module Structure

Recommended module structure:

```
MAP.M.MyModule/
├── Pages/
│   ├── MyPage.razor
│   └── MyPage.razor.cs (optional code-behind)
├── Components/
│   └── MyComponent.razor
├── Localization/
│   ├── vi.json
│   └── en.json
├── _Imports.razor
└── MAP.M.MyModule.csproj
```

---

## Testing Rules

### Test in isolation

Test your module:

1. With missing dependencies (should show error, not crash)
2. With invalid data (should show error, not crash)
3. With slow network (should handle timeouts gracefully)

### Don't commit faulty test modules

Test modules that intentionally throw errors should:

- Be in a separate test project
- Not be deployed to production
- Be clearly marked as test-only

---

## Summary

| Rule | Do | Don't |
| ------ | ----- | ------- |
| References | MAP.C.Contract, MAP.C.UI, Radzen.Blazor | MAP.C.Wpf, MAP.C.Wasm, Hosts |
| Async | `await`, explicit fire-and-forget | `async void`, `_ = task` |
| Process | Use Core contracts | `Environment.Exit`, `Process.Kill` |
| Errors | Throw, let framework handle | Swallow silently, show raw errors |
| Localization | `ILanguageService.T()` | Hardcoded strings |
| Navigation | `IPageNavigator` | Custom navigation UI |
| UI | Radzen components | Competing frameworks |
| Config | `IAppConfigService` | Direct file access |
| Logging | `ILogger` | Console.WriteLine, Debug.WriteLine |
| Sensitive data | Never log passwords/tokens | Log everything |
