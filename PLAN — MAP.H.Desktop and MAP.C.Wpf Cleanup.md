# PLAN — MAP.H.Desktop and MAP.C.Wpf Cleanup

## 1. Goal

Clean up the MAP Desktop architecture while preserving the current behavior.

Core principles:

- `MAP.H.Desktop` must remain a very thin executable host.
- Desktop runtime implementation belongs in `MAP.C.Wpf`.
- Shared Blazor shell/UI belongs in `MAP.C.UI`.
- Do not introduce unnecessary abstractions.
- Do not redesign unrelated architecture.
- Do not change the current Module loading architecture in this cleanup.
- Work incrementally.
- Build and verify after each story before continuing.

Target dependency structure:

```text id="u8qkaj"
MAP.H.Desktop
    │
    ▼
MAP.C.Wpf
    ├── MAP.C.UI
    ├── MAP.C.Runtime
    └── MAP.C.Contract
```

Target Desktop startup flow:

```text id="scx6ly"
WPF Application
    ↓
WpfHost
    ↓
MainWindow / BlazorWebView
    ↓
MAP.C.UI.Layout.MainLayout
    ↓
IPageNavigator
    ↓
Module
```

Desktop should not have a separate Blazor Router layer.

---

# STORY 1 — Remove the unnecessary Desktop Razor layer

## Objective

Remove Razor components from `MAP.H.Desktop` that no longer provide useful behavior.

## Delete

```text id="434xqf"
MAP.H.Desktop/DesktopApp.razor
MAP.H.Desktop/_Imports.razor

MAP.H.Desktop/Pages/Home.razor
MAP.H.Desktop/Pages/NotFound.razor
```

Delete the `Pages` directory if it becomes empty.

## Change Desktop startup

Current:

```csharp id="y3uai0"
WpfHost.Run(this, typeof(DesktopApp));
```

Target:

```csharp id="iu3qfw"
WpfHost.Run(this);
```

`MAP.C.Wpf.WpfHost` must internally use:

```csharp id="wqnzqj"
typeof(MAP.C.UI.Layout.MainLayout)
```

as the Desktop root component.

`MAP.H.Desktop` should not need to know about `MainLayout`.

## Do not introduce

Do not create abstractions such as:

```text id="h5nytn"
IDesktopRootComponent
IRootComponentProvider
DesktopRootFactory
```

The root component is currently a framework implementation detail and does not require an abstraction.

## Acceptance Criteria

- Desktop builds successfully.
- Application starts normally.
- Header renders normally.
- Menu renders normally.
- Default page behavior is unchanged.
- Empty page state still works.
- Module navigation works.
- Back navigation works.
- Module errors remain isolated.
- No source reference to `DesktopApp` remains.

---

# STORY 2 — Normalize the BlazorWebView HostPage

## Objective

Use the standard Blazor Hybrid static asset structure.

Move:

```text id="ue0lwi"
MAP.H.Desktop/index.html
```

to:

```text id="uvyg5j"
MAP.H.Desktop/wwwroot/index.html
```

## Update MainWindow.xaml

Change:

```xml id="60mhzp"
HostPage="index.html"
```

to:

```xml id="utl18s"
HostPage="wwwroot/index.html"
```

## Project SDK

Keep:

```xml id="aevsnb"
<Project Sdk="Microsoft.NET.Sdk.Razor">
```

Do NOT change the Desktop project to:

```xml id="qs8ash"
Microsoft.NET.Sdk
```

Keep:

```xml id="dq9kb1"
<UseWPF>true</UseWPF>
```

## Remove publish workaround

Review and remove the custom target:

```xml id="oh26si"
<Target Name="FlattenWwwrootForPublish" ...>
```

if it is no longer required after using:

```text id="ykle10"
wwwroot/index.html
```

Do not preserve custom MSBuild behavior unless it is actually necessary.

## Verification

Run at minimum:

```powershell id="5d58rh"
dotnet build
```

and:

```powershell id="quvsd9"
dotnet publish
```

Then run the application from the publish output directory.

## Acceptance Criteria

- `BlazorWebView` loads successfully.
- `wwwroot/index.html` is found.
- Radzen CSS loads.
- Radzen JavaScript loads.
- Shared CSS loads.
- Fonts load.
- `app-log.js` loads.
- `_content/...` static assets work.
- Published application starts correctly.
- `FlattenWwwrootForPublish` is no longer required.

---

# STORY 3 — Remove the H.Desktop → H.Web asset dependency

## Objective

Hosts must not depend on each other.

The following dependency is forbidden:

```text id="qvj0ir"
MAP.H.Desktop
    ↓
MAP.H.Web
```

Both hosts should consume shared assets instead.

## Current problem

`MAP.H.Desktop.csproj` currently obtains Tailwind CSS from:

```text id="asxrz1"
MAP.H.Web/wwwroot/css/tailwind.css
```

Remove this dependency.

## Target shared style structure

Use:

```text id="drkrwy"
Shared/Styles/
├── app.css
├── inter.css
├── tailwind-input.css
├── tailwind.config.js
└── tailwind.css
```

`tailwind.css` is generated output and remains committed to the repository.

## Update build-css.ps1

Change Tailwind output from:

```text id="k2znxs"
MAP.H.Web/wwwroot/css/tailwind.css
```

to:

```text id="7hw9t1"
Shared/Styles/tailwind.css
```

There must be one canonical generated Tailwind file.

## Desktop assets

`MAP.H.Desktop.csproj` should copy/link the required assets from Shared into its `wwwroot` output:

```text id="ifm46m"
Shared/Styles/app.css
Shared/Styles/inter.css
Shared/Styles/tailwind.css

Shared/Fonts/Inter/*
Shared/Scripts/app-log.js
```

Preserve the final public asset paths expected by `index.html`.

For example:

```text id="xxp2kg"
wwwroot/css/app.css
wwwroot/css/inter.css
wwwroot/css/tailwind.css
wwwroot/fonts/inter/*
wwwroot/js/app-log.js
```

## Web assets

`MAP.H.Web` should consume the same canonical Shared assets instead of maintaining duplicate canonical copies.

## Do not create

Do not introduce a new project such as:

```text id="ljzjff"
MAP.C.Assets
MAP.Shared.WebAssets
MAP.C.StaticAssets
```

Use the existing `Shared` structure.

## Acceptance Criteria

Repository search must show no asset dependency:

```text id="q9kpj9"
MAP.H.Desktop → MAP.H.Web
```

Desktop and Web must still use the same:

- Inter font
- Tailwind output
- common MAP application styles

`build-css.ps1` generates exactly one canonical Tailwind output.

---

# STORY 4 — Make MAP.H.Desktop references minimal

## Objective

Reduce direct dependencies from the executable host.

Target:

```text id="qgshau"
MAP.H.Desktop
    ↓
MAP.C.Wpf
```

## Remove unnecessary project references

After `WpfHost.Run()` owns the root component selection, review these direct references in `MAP.H.Desktop`:

```text id="v7clcq"
MAP.C.Contract
MAP.C.Runtime
MAP.C.UI
```

Remove them if `MAP.H.Desktop` no longer directly uses their APIs.

Keep:

```text id="3rvxfc"
MAP.C.Wpf
```

The final dependency graph should rely on `MAP.C.Wpf` to bring its required dependencies.

## Radzen dependency

`MAP.C.Wpf/WpfServices.cs` directly calls Radzen APIs such as:

```csharp id="uvd9p2"
services.AddRadzenComponents();
```

Therefore `MAP.C.Wpf.csproj` should explicitly declare:

```xml id="9ndtuj"
<PackageReference Include="Radzen.Blazor" />
```

Do not rely on an accidental transitive package dependency through `MAP.C.UI`.

After doing this, determine whether `MAP.H.Desktop` still requires its direct `Radzen.Blazor` package reference.

If it does not directly use Radzen APIs, remove it.

## Rule

A project that directly uses an external API should directly declare the corresponding package dependency.

## Acceptance Criteria

`MAP.H.Desktop.csproj` is reduced to the minimum dependencies required by the executable host.

Host runtime service registration must remain in `MAP.C.Wpf`, not in `MAP.H.Desktop`.

---

# STORY 5 — Review and simplify Desktop static assets

## Objective

Keep `wwwroot/index.html` small and predictable while preserving all current behavior.

## Required elements

Keep a Blazor root:

```html id="8wl5d9"
<div id="app"></div>
```

Keep the Blazor Hybrid runtime:

```html id="5h2n8f"
<script src="_framework/blazor.webview.js"></script>
```

Keep required Radzen static assets.

Keep required MAP shared styles/scripts.

## Splash screen

Preserve the existing startup splash behavior.

Do not redesign the splash screen in this story.

Small cleanup is allowed only when behavior remains identical.

## Startup error UI

Preserve the current startup/error feedback.

Do not introduce a new JavaScript error framework.

## Acceptance Criteria

- No abnormal white flash during normal startup.
- Splash disappears when Blazor renders.
- Slow startup feedback remains functional.
- Startup failures still provide useful feedback.
- Browser/WebView DevTools contain no important static asset 404 errors.

---

# CHECKPOINT 1

STOP after Stories 1–5.

Do not continue to runtime resilience changes until this checkpoint is verified.

Expected `MAP.H.Desktop` structure:

```text id="4wcolt"
MAP.H.Desktop/
├── App.xaml
├── App.xaml.cs
├── MAP.H.Desktop.csproj
└── wwwroot/
    └── index.html
```

There should be no:

```text id="qp7yny"
DesktopApp.razor
_Imports.razor
Pages/Home.razor
Pages/NotFound.razor
```

Expected runtime dependency:

```text id="nqv7w0"
MAP.H.Desktop
      ↓
  MAP.C.Wpf
      ↓
 ┌────┼───────┐
 UI Runtime Contract
```

Before continuing:

- build Desktop
- run Desktop
- publish Desktop
- run from publish directory
- test navigation
- test one or more Modules
- verify static assets

Record any problems found.

Do not redesign around problems discovered at this checkpoint without documenting them first.

---

# STORY 6 — Improve WPF shell resilience

This story begins only after Checkpoint 1 succeeds.

## 6.1 Prevent DB configuration failures from preventing shell startup

### Current risk

Avoid this failure chain:

```text id="aoy490"
db-api.json invalid/missing
        ↓
IDbApiClient construction fails
        ↓
IMenuService construction fails
        ↓
MainLayout cannot initialize
        ↓
Shell never becomes usable
```

This conflicts with the MAP resilience principle.

### Target behavior

The shell should be able to initialize sufficiently to:

- write useful logs
- show a controlled error
- keep Header/Menu/Shell infrastructure alive where possible
- allow access to System Config/System Logs where appropriate

### Preferred approach

Use a simple deferred/lazy initialization or controlled failure strategy.

Do not redesign the whole database subsystem.

### Do not introduce

Avoid abstractions such as:

```text id="1fzqrt"
DatabaseSubsystemManager
DatabaseProviderFactory
IPlatformDatabaseRuntime
IDatabaseBootstrapPipeline
```

unless an actual requirement makes them necessary.

---

# STORY 6.2 — Tighten global WPF exception handling

## Current concern

Review:

```csharp id="x5flnf"
Application.DispatcherUnhandledException
```

Do not classify almost every Dispatcher exception as safely recoverable.

## Target philosophy

```text id="dk4jhp"
Module failure
    ↓
ModuleErrorBoundary
    ↓
Log + isolate Module + continue

Unknown WPF/Core failure
    ↓
Log completely
    ↓
Fail clearly unless explicitly known to be recoverable
```

Only set:

```csharp id="zwf40c"
e.Handled = true;
```

for exception types/scenarios where MAP knows the application remains in a valid state.

Do not swallow unknown infrastructure failures.

---

# STORY 6.3 — Make application shutdown deterministic

Review the current pattern:

```csharp id="wsjp42"
application.Exit += async (_, _) =>
{
    await host.StopAsync();
    ...
};
```

The WPF event signature results in `async void` behavior.

Improve shutdown so that it is deterministic and simple.

Goals:

- stop the host correctly
- dispose once
- flush logging as reliably as practical
- avoid hanging during shutdown
- avoid unnecessary lifecycle abstractions

Do not create a generic application lifecycle framework.

---

# STORY 6.4 — Improve AppConfigService logging

`MAP.C.Wpf.Config.AppConfigService` should use:

```csharp id="hjh27u"
ILogger<AppConfigService>
```

for operational failures.

Replace relevant `Debug.WriteLine` usage, especially for:

- invalid/corrupt config
- config load failures
- restart failures

The log should contain enough path/context information to diagnose the issue.

`FileLogStore` may still use `Debug.WriteLine` internally when the logging backend itself fails, to avoid recursive logging.

---

# STORY 6.5 — Remove localized UI strings from DisplayHelper

`MAP.C.Wpf.DisplayHelper` must not generate hardcoded user-facing Vietnamese strings such as:

```text id="dc70pf"
Màn hình 1
(Chính)
```

Platform code should provide display information.

UI/localization code should generate localized labels.

Desired conceptual separation:

```text id="r15c3v"
MAP.C.Wpf
→ display facts

MAP.C.UI
→ localized display text
```

Keep this change small.

Do not redesign `DisplayInfo` more than necessary.

---

# STORY 7 — Desktop Module packaging cleanup

## Objective

Keep Module build/copy responsibility in the executable Host while improving consistency.

## Keep

The Desktop host project should continue to:

```text id="r8fml3"
discover Modules/*
build Module projects
copy Module DLLs into Desktop output
```

This is packaging responsibility and belongs in the executable project.

Do not move it into `MAP.C.Wpf`.

## Module output directory

Review casing and naming:

```text id="sfrlm2"
modules/
```

versus:

```text id="66xzu6"
Modules/
```

Prefer one convention across:

- Desktop
- Web
- scripts
- documentation
- future Developer Kit

Do not change casing during this cleanup if it creates deployment risk without meaningful benefit.

Consistency is more important than a specific casing choice.

## Debug symbols

For Debug/developer builds, also copy Module:

```text id="vdg7o6"
*.pdb
```

when practical.

This improves internal stack traces and debugging.

Production behavior must remain unchanged.

---

# STORY 8 — Tests and verification

Do not pursue broad test coverage.

Focus on behavior that protects MAP architecture.

## Desktop startup

Verify:

```text id="yqczn8"
Application starts
MainWindow appears
WebView2 initializes
MainLayout renders
```

## Static assets

Verify there are no important 404 failures for:

```text id="o21e5b"
Radzen CSS
Radzen JavaScript
Inter fonts
Tailwind CSS
app.css
app-log.js
```

## Navigation

Test:

```text id="8zla62"
open Module page
switch Module
open same page
navigate with parameters
back navigation
```

## Module failure

Test at least:

```text id="va987b"
missing Module DLL
missing component type
Module render exception
```

Expected:

```text id="cx8cnf"
Header survives
Menu survives
Shell survives
Error is logged
Useful error feedback is shown
```

## Configuration failure

Test:

```text id="yrfnke"
app-config missing
app-config invalid
db-api invalid
page.json invalid
```

Failures must be clearly logged and handled according to their severity.

## Publish verification

Always run:

```powershell id="veexyr"
dotnet publish
```

and test the application from the actual publish output directory.

Do not consider the story complete based only on Visual Studio or `bin/Debug` execution.

---

# OUT OF SCOPE

Do NOT perform any of the following during this plan:

```text id="urzw36"
- redesign ModuleLoader
- introduce per-Module AssemblyLoadContext
- create a plugin framework
- create a Module manifest framework
- add CQRS
- add MediatR
- add an internal event bus
- introduce a service locator
- create a Module lifecycle framework
- redesign navigation
- change IModuleLoader API
- redesign MAP.H.Web
- rewrite localization architecture
- replace Radzen
- replace WPF
- redesign database architecture
```

If an issue outside scope is discovered:

1. Document the issue.
2. Explain the observed impact.
3. Do not redesign unrelated architecture to solve it.
4. Continue independent work when safe.
5. Include the issue in the final report.

---

# REQUIRED EXECUTION ORDER

Execute exactly in this order:

```text id="mvdvmu"
1. Remove Desktop Razor layer
2. Move HostPage to wwwroot
3. Consolidate shared assets
4. Minimize H.Desktop dependencies
5. Verify build/run/publish

--- CHECKPOINT 1 ---

6. Improve runtime resilience
7. Clean up Module packaging
8. Complete focused tests and smoke verification
```

Do not combine everything into one large change.

Prefer small, reviewable commits or logical change groups.

---

# IMPLEMENTATION RULES

While executing this plan:

- Preserve existing behavior unless explicitly requested otherwise.
- Prefer deleting unnecessary code over adding abstractions.
- Prefer framework conventions over custom MSBuild workarounds.
- Do not add abstractions for hypothetical future requirements.
- Keep changes localized.
- Do not opportunistically refactor unrelated files.
- Keep logging useful and structured.
- Do not swallow unexpected technical exceptions.
- Do not introduce duplicate logging.
- Do not weaken Module fault isolation.
- Do not create dependencies between Desktop and Web hosts.
- Keep `MAP.C.Wpf` responsible for Desktop-specific runtime behavior.
- Keep `MAP.H.Desktop` responsible for executable packaging/bootstrap only.

---

# DEFINITION OF DONE

The cleanup is complete when:

- `MAP.H.Desktop` is a genuinely thin executable host.
- Desktop no longer contains an unnecessary Blazor Router.
- `DesktopApp.razor` is removed.
- Desktop `_Imports.razor` is removed.
- Desktop `Home.razor` and `NotFound.razor` are removed.
- Desktop HostPage uses `wwwroot/index.html`.
- The custom `FlattenWwwrootForPublish` workaround is removed if no longer necessary.
- There is no `MAP.H.Desktop → MAP.H.Web` dependency.
- Shared CSS/fonts/scripts have a clear canonical source.
- `MAP.H.Desktop` has minimal project/package references.
- `MAP.C.Wpf` owns Desktop runtime behavior.
- Module loading behavior remains unchanged.
- Module failures do not break Header/Menu/Shell.
- Optional infrastructure/configuration failures do not unnecessarily prevent the shell from starting.
- Startup/config/runtime failures are clearly logged.
- Desktop works from both build output and publish output.
- No major architecture redesign was introduced outside this plan.

---

# FINAL AGENT REPORT

At the end, provide a concise report containing:

## Changed

List the files added, changed, moved, and deleted.

## Architecture

Describe the final:

```text id="bhrp3d"
MAP.H.Desktop
    ↓
MAP.C.Wpf
    ↓
MAP.C.UI / MAP.C.Runtime / MAP.C.Contract
```

dependency structure.

## Verification

Report the result of:

```text id="nayhjj"
dotnet build
dotnet test
dotnet publish
```

and any Desktop smoke testing performed.

## Remaining Issues

List only issues actually discovered during implementation.

Do not propose speculative redesigns unless they are required to explain a concrete remaining problem.