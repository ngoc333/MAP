# PLAN — Stabilize and Simplify MAP Web/Desktop Runtime

## Scope

Work only in:

```text
MAP.H.Web
MAP.H.Desktop
Core/MAP.C.Wasm
Core/MAP.C.Wpf
```

Small supporting changes to shared asset references are allowed only when required by `MAP.H.Web` or `MAP.H.Desktop`.

Do NOT modify:

```text
build*.ps1
deploy*.ps1
MAP.H.Web.Host
Run-App
Module architecture
navigation contracts
MAP.C.Runtime architecture
```

Do not redesign unrelated parts of MAP.

---

# Architecture Goal

Both executable/application hosts should follow the same principle:

```text
MAP.H.Desktop
      ↓
  MAP.C.Wpf
      ↓
 shared runtime/UI

MAP.H.Web
      ↓
 MAP.C.Wasm
      ↓
 shared runtime/UI
```

The Hosts should contain platform entry points and static assets/package declarations.

Real Desktop/Web startup behavior belongs in:

```text
MAP.C.Wpf
MAP.C.Wasm
```

Modules must remain unaware of WPF versus WebAssembly.

---

# PRIORITY ORDER

```text
P0  Restore Web CSS/static assets
P1  Make H.Web thin like H.Desktop
P1  Move Web startup/runtime bootstrap into C.Wasm
P1  Make Web DB configuration failure recoverable
P1  Make WPF page.json failure recoverable
P1  Stop swallowing unknown WPF Dispatcher exceptions
P2  Clean shared Inter assets
P2  Clean Wasm menu fallback/restart behavior
P2  Fix overlapping ModuleLoader loading state
```

Do the work in this order.

---

# STORY 1 — Fix the Web CSS regression first

## Problem

`MAP.H.Web/wwwroot/index.html` requests:

```text
css/tailwind.css
```

The canonical file is now:

```text
Shared/Styles/tailwind.css
```

but `MAP.H.Web.csproj` only declares it as a linked `None` item.

Unlike shared config/scripts, Tailwind is not currently registered as a proper Web static asset.

This is the first problem to fix.

## Required change

Register:

```text
Shared/Styles/tailwind.css
```

in the Blazor static web asset pipeline and expose it as:

```text
css/tailwind.css
```

Use the same existing mechanism already used in `MAP.H.Web.csproj` for:

```text
page.json
db-api.json
app-log.js
app-config.js
```

Do not restore another canonical copy under:

```text
MAP.H.Web/wwwroot/css/tailwind.css
```

The source of truth remains:

```text
Shared/Styles/tailwind.css
```

## Verify

Run Web normally.

Verify:

```text
GET /css/tailwind.css → 200
GET /css/app.css      → 200
GET /css/inter.css    → 200
```

Verify Tailwind classes are actually applied:

```text
flex
flex-col
h-screen
w-screen
bg-gray-50
p-6
w-64
overflow-hidden
```

## Acceptance Criteria

The Web UI visually matches Desktop/pre-refactor behavior again.

No important CSS 404.

Stop here until Web styling is correct.

---

# STORY 2 — Review and remove the redundant Web Router layer

## Objective

Determine whether Web routing currently has any real MAP responsibility.

Before changing code, search the repository for:

```text
@page
NavigationManager
NavigateTo
Router
RouteView
FocusOnNavigate
```

Pay special attention to Modules.

## Expected current architecture

MAP page navigation is:

```text
IPageNavigator
    ↓
MainLayout
    ↓
PageContainer
    ↓
DynamicComponent
```

and not URL-based Blazor page navigation.

If repository inspection confirms that:

```text
MAP.H.Web/Pages/Home.razor
MAP.H.Web/Pages/NotFound.razor
MAP.H.Web/App.razor
```

exist only to create the Web root Router, remove them.

Also remove:

```text
MAP.H.Web/_Imports.razor
```

if H.Web has no Razor source remaining.

## Do not remove Router if

There is an actual production URL/deep-link behavior outside Home/NotFound that depends on Blazor routing.

If such a dependency exists, document it and leave Router intact.

Do not redesign routing during this story.

## Target

Prefer:

```text
Browser
  ↓
MAP.C.UI.Layout.MainLayout
  ↓
IPageNavigator
  ↓
Module
```

instead of:

```text
Browser
  ↓
App Router
  ↓
Home route
  ↓
MainLayout
  ↓
IPageNavigator
  ↓
Module
```

---

# STORY 3 — Introduce WasmHost and make H.Web thin

## Objective

Mirror the successful Desktop pattern.

Current Desktop:

```csharp
public App() => WpfHost.Run(this);
```

Target Web `Program.cs` should be conceptually equivalent:

```csharp
using MAP.C.Wasm;

await WasmHost.RunAsync(args);
```

Do not require H.Web to understand runtime initialization.

## Add

Create:

```text
Core/MAP.C.Wasm/WasmHost.cs
```

`WasmHost` should own:

```text
WebAssemblyHostBuilder creation
root component registration
HeadOutlet registration
base HttpClient creation
AddWasm(...)
database configuration registration
host.Build()
language initialization
AppConfigService initialization
startup logging
host.RunAsync()
```

## Root component

If Story 2 confirms Router is unnecessary, register:

```csharp
MainLayout
```

directly as the root component.

Conceptually:

```csharp
builder.RootComponents.Add<MainLayout>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");
```

The root component must be selected by `MAP.C.Wasm`, not by H.Web.

Do not pass a root component Type from H.Web unless repository evidence proves multiple Web roots are required.

Avoid unnecessary configurability.

---

# STORY 4 — Minimize H.Web dependencies

After `WasmHost` owns runtime startup, clean `MAP.H.Web.csproj`.

## Project references

Target direct Core reference:

```text
MAP.H.Web
    ↓
MAP.C.Wasm
```

Remove direct references to:

```text
MAP.C.Contract
MAP.C.Runtime
MAP.C.UI
```

if H.Web no longer directly uses them.

Keep Module project references required for Web lazy loading/package discovery.

Those references are packaging requirements, not application architecture.

## Packages

Do not aggressively remove packages during CSS stabilization.

`Radzen.Blazor` may remain directly referenced by H.Web because `index.html` directly consumes Radzen static web assets.

However `MAP.C.Wasm` itself directly uses:

```csharp
AddRadzenComponents()
RadzenLocalizer
```

Therefore `MAP.C.Wasm.csproj` must explicitly declare:

```xml
<PackageReference Include="Radzen.Blazor" />
```

Do not rely on Radzen arriving accidentally through `MAP.C.UI`.

Rule:

```text
Direct API use
→ direct package reference
```

---

# CHECKPOINT 1 — Web architecture

Stop here.

Expected Web structure should be approximately:

```text
MAP.H.Web/
├── Program.cs
├── MAP.H.Web.csproj
└── wwwroot/
    ├── index.html
    ├── css/
    │   └── app.css
    └── ...
```

Prefer no:

```text
App.razor
_Imports.razor
Pages/Home.razor
Pages/NotFound.razor
```

unless Story 2 proves Router is required.

Expected startup:

```text
Program.cs
   ↓
WasmHost.RunAsync(args)
   ↓
MainLayout
```

Verify Web before continuing.

---

# STORY 5 — Make Web DB configuration failure recoverable

## Current problem

Web currently loads:

```text
db-api.json
```

before the host is fully built.

A missing, invalid, or malformed `db-api.json` can prevent Web startup completely.

Desktop already has a fallback DB client and can keep the shell alive.

Web should have equivalent resilience.

## Required behavior

In `WasmHost`:

```text
try load db-api.json
    ↓
valid
    → register real DbApiClient

invalid/missing
    → register unavailable/fallback IDbApiClient
    → log error after host/logger is available
    → continue shell startup
```

Implement a small Web-specific fallback client in:

```text
MAP.C.Wasm/Database/
```

if required.

Do not create a new database abstraction framework.

Do not make `MAP.C.Wasm` depend on `MAP.C.Wpf`.

## Important

The fallback client must fail individual DB operations clearly.

It must not make DI construction fail.

Expected:

```text
db-api.json bad
    ↓
Web shell starts
    ↓
local/system menu remains available
    ↓
DB operation reports controlled error
```

---

# STORY 6 — Clean Wasm menu fallback

## Current issue

When `page.json` fails, Web currently creates a synthetic menu containing:

```text
Trang chủ
└── Dashboard
```

but that fallback Dashboard has no real Assembly/Component.

This creates UI that looks navigable but is not actually a valid Module page.

It also hardcodes Vietnamese in platform runtime code.

## Required change

When local menu configuration cannot be loaded:

```text
log warning/error
    ↓
use empty PageConfig
    ↓
let existing SystemMenus.EnsureRegistered(...)
    provide recovery/system pages
```

Do not invent fake business pages.

Do not hardcode user-facing Vietnamese fallback menu labels in `MAP.C.Wasm`.

Target concept:

```csharp
new PageConfig
{
    Menus = []
};
```

then use the existing menu resolution path.

## Acceptance Criteria

With invalid/missing `page.json`:

```text
Web shell starts
System menu remains available
No fake Dashboard appears
Error is logged
```

---

# STORY 7 — Make WPF page.json failure recoverable

## Current problem

WPF currently does:

```csharp
File.OpenRead(page.json)
```

without controlled fallback.

Missing/corrupt `page.json` can cause:

```text
MenuService.LoadMenusAsync
    ↓
MainLayout.OnInitializedAsync
    ↓
root shell failure
```

## Required change

Make WPF behavior equivalent to the corrected Wasm MenuService:

```text
try load local PageConfig

success
→ resolve normal menus

failure
→ log error
→ use empty PageConfig
→ SystemMenus.EnsureRegistered(...)
→ shell survives
```

Do not invent fake menu entries.

Do not swallow the error silently.

## Acceptance Criteria

Test:

```text
page.json deleted
page.json malformed
page.json empty
```

Expected:

```text
MainLayout renders
Header renders
System menu is usable
Error is logged
```

---

# STORY 8 — Correct global WPF exception policy

## Current problem

`WpfHost` currently handles nearly every Dispatcher exception.

Existing behavior is effectively:

```text
unless OOM / StackOverflow / AccessViolation
→ e.Handled = true
```

This can leave MAP running after an unknown Core/WPF infrastructure fault.

## Required policy

Use:

```text
Module fault
→ ModuleErrorBoundary
→ log
→ isolate Module
→ application continues

Known explicitly recoverable WPF fault
→ log
→ e.Handled = true

Unknown Dispatcher/Core fault
→ log
→ do NOT mark handled
→ normal process failure behavior
```

At this stage there may be zero known global Dispatcher exceptions that are safe to continue from.

That is acceptable.

Prefer:

```text
default = not handled
```

Do not build an exception classification framework.

Remove `IsRecoverableException()` if it no longer provides meaningful value.

## Error presentation

Do not label every runtime Dispatcher exception:

```text
MAP startup error
```

Startup exceptions and runtime Dispatcher exceptions are different.

Keep error reporting simple and diagnostic.

---

# CHECKPOINT 2 — Platform failure behavior

Verify separately:

## Web

```text
missing db-api.json
invalid db-api.json
missing page.json
invalid page.json
```

Expected shell survival.

## Desktop

```text
missing db-api.json
invalid db-api.json
missing page.json
invalid page.json
```

Expected shell survival for these configuration faults.

Unknown WPF infrastructure faults should not be globally swallowed.

---

# STORY 9 — Clean Web AppConfig restart behavior

## Current issue

`MAP.C.Wasm.Config.AppConfigService.RestartApp()` currently does:

```csharp
_ = RestartAsync();
```

This is discarded fire-and-forget.

Avoid this pattern.

## Preferred solution

Because this implementation runs specifically inside Blazor WebAssembly, use synchronous in-process JS interop when available.

Conceptually:

```csharp
if (_js is IJSInProcessRuntime js)
{
    js.InvokeVoid("location.reload");
}
```

Wrap it in normal error logging.

Do not change `IAppConfigService` just for this issue unless absolutely necessary.

Do not introduce a lifecycle abstraction.

## Acceptance Criteria

No discarded Task is used for restart.

Reload behavior remains unchanged.

Failure is logged.

---

# STORY 10 — Shared Inter assets

Do this only after Web is completely stable.

## Current issue

Inter files currently exist both in Shared and Host directories.

Use Shared as source of truth.

Canonical:

```text
Shared/Styles/inter.css
Shared/Fonts/Inter/*.woff2
```

## Web

Expose shared Inter CSS/font files as proper static Web assets:

```text
/css/inter.css
/fonts/inter/*.woff2
```

## Desktop

Link/copy the same shared files into:

```text
wwwroot/css/inter.css
wwwroot/fonts/inter/
```

through `MAP.H.Desktop.csproj`.

After verification, remove duplicated physical copies from Host directories.

## Keep app.css separate

Do NOT combine:

```text
MAP.H.Web/wwwroot/css/app.css
MAP.H.Desktop/wwwroot/css/app.css
```

in this pass.

They contain platform/startup-specific styling and do not need forced deduplication.

---

# STORY 11 — Review ModuleLoader overlapping load state

This item was raised in the original architecture review and was intentionally deferred.

Now review whether it can occur through rapid navigation.

Both WPF and Wasm currently expose:

```csharp
event Action<bool>? OnLoadingChanged;
```

and perform:

```text
Load A → true
Load B → true
Load A ends → false
Load B still running
```

The UI may therefore stop showing the loading state too early.

## Required minimum fix

Keep the existing public contract.

Internally replace simple true/false toggling with an active load counter.

Concept:

```text
first active load
→ OnLoadingChanged(true)

additional load
→ no state change

load completes
→ decrement

last active load completes
→ OnLoadingChanged(false)
```

Never allow the count to go below zero.

Apply the same semantics to:

```text
MAP.C.Wpf.Modules.ModuleLoader
MAP.C.Wasm.Modules.ModuleLoader
```

Do not create a shared base ModuleLoader.

The two platform loaders should remain separate.

---

# STORY 12 — Review duplicate concurrent Module loads

This is a second-level P2 item.

Both loaders currently use:

```text
Dictionary<string, Assembly>
Dictionary<string, Type>
```

and a check-then-load sequence.

Two overlapping calls for the same uncached assembly can both begin loading/localization.

## Only fix if reproducible or clearly possible

If rapid navigation can cause this in current execution:

Use a small in-flight task cache, conceptually:

```text
assembly name
    ↓
Task<Assembly>
```

Requirements:

```text
one assembly load/localization operation at a time per assembly
successful load → normal cache
failed load → remove in-flight entry so retry is possible
```

Do not introduce:

```text
AssemblyLoadContext
plugin lifecycle
module manager
loader framework
```

If current execution guarantees serialization, document it and leave this story unchanged.

---

# EXPLICITLY DEFERRED ITEM — Display localization

The original review also identified that `DisplayHelper` generates user-facing strings such as:

```text
Display 1 (Primary)
```

and Wasm returns:

```text
Browser
```

from platform code.

This issue is still real.

However, fixing it correctly requires responsibility to move toward the UI/localization layer and may require changing how `DisplayInfo.Name` is consumed.

That crosses beyond the four-project stabilization scope.

Therefore:

```text
DO NOT "fix" it by translating strings inside C.Wpf/C.Wasm.
DO NOT add more hardcoded localized strings.
DO NOT redesign DisplayInfo in this pass.
```

Record it as a follow-up architecture item.

The previous agent change from Vietnamese to English did not actually solve the architectural issue.

---

# EXPLICITLY ALREADY COMPLETED

Do not redo these items.

## H.Desktop thin host

Already correct:

```text
App.xaml.cs
→ WpfHost.Run(this)
```

Keep it.

## Desktop root Router removal

Already correct.

Do not restore:

```text
DesktopApp.razor
Desktop Home
Desktop NotFound
Desktop _Imports.razor
```

## WPF DB configuration fallback

Already improved through fallback `IDbApiClient`.

Review behavior only; do not redesign.

## AppConfigService structured logging

Already improved.

Do not replace `ILogger` with another logging abstraction.

## Radzen dependency in C.Wpf

Already made explicit.

Keep it.

---

# DO NOT CHANGE

Do not touch:

```text
build.ps1
build-all.ps1
deploy.ps1
deploy-all.ps1
MAP.H.Web.Host
Run-App
```

Do not introduce:

```text
MediatR
CQRS
event bus
service locator
plugin framework
AssemblyLoadContext
module lifecycle framework
generic PlatformHost base class
shared Wpf/Wasm ModuleLoader base class
new asset project
```

Do not merge WPF and Wasm implementations only to eliminate small duplication.

Platform-specific duplication is acceptable when it keeps behavior obvious and debuggable.

---

# REQUIRED EXECUTION ORDER

```text
1. Fix H.Web Tailwind static asset
2. Verify Web CSS

--- CHECKPOINT ---

3. Review/remove Web Router layer
4. Add WasmHost
5. Thin H.Web references
6. Add explicit Radzen dependency to C.Wasm
7. Verify Web startup/navigation

--- CHECKPOINT ---

8. Make Web DB config failure recoverable
9. Clean Wasm page.json fallback
10. Make WPF page.json failure recoverable
11. Correct WPF Dispatcher exception policy
12. Fix Wasm restart fire-and-forget
13. Verify failure scenarios

--- CHECKPOINT ---

14. Consolidate Inter assets
15. Fix ModuleLoader loading counter
16. Review duplicate concurrent assembly loads
17. Final verification
```

Do not combine all stories into one commit.

---

# SUGGESTED COMMITS

```text
fix(web): restore shared Tailwind static asset

refactor(web): move runtime bootstrap into WasmHost

refactor(web): remove redundant router host layer

fix(wasm): preserve shell when startup configuration fails

fix(wpf): preserve shell when local menu configuration fails

fix(wpf): stop swallowing unknown dispatcher exceptions

fix(wasm): remove fire-and-forget application restart

refactor(assets): share Inter assets across hosts

fix(modules): preserve loading state across overlapping loads
```

Keep commits logical and reviewable.

---

# VERIFICATION

## H.Web

Verify:

```text
application starts
Tailwind loads
Inter loads
Radzen loads
Header renders
Menu renders
System Config works
System Logs works
module opens
module switch works
back navigation works
language switching works
```

## H.Desktop

Verify the same functional shell behavior.

## Failure scenarios — Web

Test:

```text
missing db-api.json
invalid db-api.json
missing page.json
invalid page.json
missing module DLL
invalid component name
module render failure
```

## Failure scenarios — Desktop

Test:

```text
missing db-api.json
invalid db-api.json
missing page.json
invalid page.json
missing module DLL
invalid component name
module render failure
```

Expected recoverability:

```text
configuration problem
→ shell survives when possible

module problem
→ shell survives

unknown WPF/Core infrastructure problem
→ log and fail normally, not silently continue
```

## Rapid navigation

Quickly select multiple Module pages.

Verify:

```text
loading indicator remains active until all active loads finish
final selected page is consistent
no duplicate localization failure
no shell crash
```

---

# FINAL TARGET

## Desktop

```text
MAP.H.Desktop
│
└── WpfHost.Run()
       │
       ▼
    MAP.C.Wpf
       │
       ▼
    MainLayout
```

## Web

```text
MAP.H.Web
│
└── WasmHost.RunAsync()
       │
       ▼
    MAP.C.Wasm
       │
       ▼
    MainLayout
```

Both platforms then converge at:

```text
MainLayout
    ↓
IPageNavigator
    ↓
PageContainer
    ↓
ModuleErrorBoundary
    ↓
Dynamic Module
```

That is the architecture this cleanup should preserve.