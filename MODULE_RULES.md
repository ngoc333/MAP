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

Exception: Only for external/native .NET event signatures that truly require `void`.
Blazor UI event handlers should use `async Task`, not `async void`.

### Do NOT discard fire-and-forget tasks

```csharp
// WRONG - exception is silently lost
_ = SomeAsync();

// CORRECT - await the task
await SomeAsync();
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

### Expected/business errors

Handle these within the Module:

- Validation errors
- Invalid user input
- Business rule violations
- Expected API results (not found, access denied)
- User action not allowed

Use appropriate patterns:

```csharp
// Show validation message
notificationService.Notify(...);
// Return result
return Result.Fail("...");
// Show dialog
await dialogService.Alert(...);
```

Do NOT push business errors into ErrorBoundary.

### Unexpected technical exceptions

These include:

- `NullReferenceException`
- Unexpected service failure
- Render/lifecycle bugs
- Uncaught exceptions

Do NOT swallow these. Let Core fault-isolation handle them:

- Log with ErrorId
- Show notification to user
- Isolate the failed module

### Throw meaningful exceptions

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

### Module Page normal UI navigation

Use `OpenPageAsync` from `BasePage` for button/link/event navigation:

```csharp
// GOOD — safe navigation, error handled gracefully
await OpenPageAsync("target");
await OpenPageAsync("target", new { Id = 1 });
```

Do NOT use raw `Navigator.OpenAsync(...)` for normal UI navigation in BasePage.

### Back navigation

```csharp
await Navigator.BackAsync();
```

This is acceptable — it's not a module load operation.
Prefer shell-provided navigation UI when available.

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

### Avoid duplicate logging

Module may use `ILogger` but should not full-log and then rethrow if Core
will log again at the containment boundary. Avoid unnecessary duplicate logs.

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
| Async | `async Task`, `await` | `async void`, `_ = task` |
| Process | Use Core contracts | `Environment.Exit`, `Process.Kill` |
| Errors | Business: handle in Module; Technical: let Core isolate | Swallow technical errors, push business errors to ErrorBoundary |
| Localization | `ILanguageService.T()` | Hardcoded strings |
| Navigation | `OpenPageAsync` from BasePage | Raw `Navigator.OpenAsync` for UI events |
| UI | Radzen components | Competing frameworks |
| Config | `IAppConfigService` | Direct file access |
| Logging | `ILogger`, avoid duplicate logs | Console.WriteLine, duplicate full-log + rethrow |
| Sensitive data | Never log passwords/tokens | Log everything |
