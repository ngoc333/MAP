# FOLLOW-UP PLAN — Finalize Web/Desktop Runtime Cleanup

## Baseline

Start from the current latest commit:

```text
f293f195750950e104d70a1e906b7b970652ba96
```

This follow-up is intentionally small.

The major architecture is already correct:

```text
MAP.H.Desktop → MAP.C.Wpf
MAP.H.Web     → MAP.C.Wasm

MAP.C.Wpf / MAP.C.Wasm
        ↓
     MAP.C.UI
        ↓
    MainLayout
        ↓
  IPageNavigator
        ↓
   ModuleLoader
        ↓
      Module
```

Do not redesign this architecture.

---

# Scope

Focus only on:

```text
Core/MAP.C.Wpf
Core/MAP.C.Wasm
MAP.H.Desktop
MAP.H.Web
Shared/Styles
Shared/Fonts
```

Do NOT modify:

```text
build*.ps1
deploy*.ps1
MAP.H.Web.Host
Run-App
MAP.C.Runtime architecture
MAP.C.UI architecture
navigation contracts
module contracts
```

Small supporting changes outside the listed folders are allowed only if absolutely required to compile.

---

# PRIORITY

Execute exactly in this order:

```text
1. Fix fallback DB client semantics
2. Prevent duplicate in-flight Module loads
3. Remove raw exception details from WPF startup UI
4. Canonicalize Inter CSS/fonts
5. Final verification
```

Do not combine unrelated cleanup.

---

# STORY 1 — Make fallback DB clients fail clearly

## Problem

The Web fallback DB client currently returns:

```json
{}
```

when the DB API configuration is unavailable.

That makes an unavailable database look like a successful request with an empty response.

This is incorrect.

Expected behavior is:

```text
DB configuration unavailable
→ Shell can still start

Actual DB operation attempted
→ Fail clearly and immediately
→ Log useful diagnostic information
```

Desktop already follows the second behavior more closely, but it throws synchronously from methods returning `Task<JsonElement>`.

Make Web and Desktop semantics consistent.

---

## Web

Current location:

```text
Core/MAP.C.Wasm/WasmHost.cs
```

Prefer moving the fallback implementation into a dedicated file:

```text
Core/MAP.C.Wasm/Database/FallbackDbApiClient.cs
```

Keep it:

```text
internal sealed
```

Store the initialization failure reason.

Conceptually:

```csharp
internal sealed class FallbackDbApiClient : IDbApiClient
{
    private readonly ILogger _logger;
    private readonly string _reason;
}
```

For every operation:

```text
CallOracleAsync
CallPostgreSqlFunctionAsync
CallPostgreSqlProcedureAsync
```

log the failure and return a faulted Task.

Preferred pattern:

```csharp
return Task.FromException<JsonElement>(
    new InvalidOperationException(
        $"Database API is not configured: {_reason}"));
```

Do NOT return:

```text
{}
null
default(JsonElement)
success=false fake payload
```

The absence of DB infrastructure is a technical failure and should remain visible to callers.

---

## Desktop

Current location:

```text
Core/MAP.C.Wpf/Database/FallbackDbApiClient.cs
```

It currently performs synchronous:

```csharp
throw new InvalidOperationException(...)
```

inside methods declared as:

```csharp
Task<JsonElement>
```

Change these to the same faulted-Task semantics as Wasm.

Example:

```csharp
return Task.FromException<JsonElement>(
    new InvalidOperationException(...));
```

Keep logging before returning the faulted Task.

Do not share a base implementation between WPF and Wasm.

Small duplication is acceptable.

---

## Acceptance Criteria

With invalid/missing `db-api.json`:

```text
Web shell starts
Desktop shell starts
local/System menus remain usable
```

When a Module directly calls the DB API:

```text
operation fails with InvalidOperationException
error clearly says DB API is unavailable/not configured
error is logged
no fake empty JSON response is returned
```

---

# CHECKPOINT 1

Verify both platforms before continuing.

Test:

```text
missing db-api.json
invalid db-api.json
```

Then open:

```text
System Config
System Logs
```

Then trigger a real DB operation.

Expected:

```text
Shell survives configuration failure
DB-dependent operation fails clearly
```

---

# STORY 2 — Deduplicate concurrent Module assembly loads

## Problem

Both ModuleLoaders now correctly maintain loading-state counters.

However, overlapping navigation can still trigger duplicate physical/logical assembly loads.

Current pattern is approximately:

```csharp
if (!_loadedAssemblies.ContainsKey(assemblyName))
{
    var assembly = await LoadAssemblyAsync(...);
    await LoadLocalizationAsync(assembly);

    _loadedAssemblies[assemblyName] = assembly;
}
```

Two callers can both reach the check before either finishes the awaited work.

Example:

```text
Navigation A
→ assembly missing
→ begin load
→ await localization

Navigation B
→ assembly still missing
→ begin same load again
```

This can duplicate:

```text
assembly loading
localization loading
logging
resource initialization
```

---

## Required solution

Keep:

```text
_loadedAssemblies
_cachedTypes
_activeLoadCount
```

Add an in-flight cache per assembly.

Conceptually:

```csharp
Dictionary<string, Task<Assembly>> _inFlightAssemblyLoads;
```

or an equivalent minimal implementation.

Do not introduce a ModuleLoader framework.

---

## Required behavior

For an uncached assembly:

```text
First caller
→ creates load Task
→ registers task as in-flight

Second caller for same assembly
→ awaits the existing Task

Load succeeds
→ commit Assembly to normal cache
→ remove in-flight entry

Load fails
→ remove in-flight entry
→ propagate exception
→ next request may retry
```

Do not permanently cache failed tasks.

---

## Thread/concurrency safety

The current dictionaries are not inherently safe for concurrent mutation.

Because this code now intentionally handles overlapping operations, protect access to:

```text
in-flight dictionary
assembly cache
type cache where required
```

with a small and understandable mechanism.

Preferred options:

```text
lock
SemaphoreSlim
ConcurrentDictionary where it materially simplifies code
```

Do not overengineer.

Avoid locking across long `await` operations unless the design intentionally uses a `Task` as the shared in-flight value.

A good pattern is:

```text
short lock
→ get/create Task
→ release lock
→ await Task
```

---

## WPF specifics

File:

```text
Core/MAP.C.Wpf/Modules/ModuleLoader.cs
```

The shared in-flight task must include:

```text
Assembly.LoadFrom
module localization initialization
successful assembly cache commit
```

Do not cache the assembly before localization succeeds.

Preserve current retry semantics.

---

## Wasm specifics

File:

```text
Core/MAP.C.Wasm/Modules/ModuleLoader.cs
```

The in-flight task must include:

```text
LazyAssemblyLoader.LoadAssembliesAsync
assembly validation
module localization initialization
successful assembly cache commit
```

Do not cache before localization succeeds.

---

## Loading state

Keep the current `_activeLoadCount` behavior.

Do not replace it.

Each public call to:

```csharp
LoadComponentAsync(...)
```

still counts as an active operation.

Example:

```text
Request A starts → loading true
Request B starts → still true

both share same assembly load task

Request A finishes
Request B finishes
→ loading false only after final caller completes
```

---

## Acceptance Criteria

Rapidly open pages from the same previously-unloaded Module.

Expected logs should show:

```text
one assembly load
one localization load
multiple component/type resolutions allowed
```

There must be no:

```text
duplicate resource initialization
duplicate assembly load caused by overlap
negative loading count
stuck loading indicator
```

If the shared load fails:

```text
all waiting callers receive failure
in-flight cache is cleared
next navigation can retry
```

---

# CHECKPOINT 2

Test both WPF and Wasm.

Use rapid navigation to two pages in the same Module.

Test:

```text
A → Module X / Page 1
B → Module X / Page 2
```

before Module X has been loaded.

Verify:

```text
one Module X assembly load
one localization initialization
loading indicator remains correct
final navigation does not crash shell
```

Also intentionally cause a module load failure and retry.

---

# STORY 3 — Do not show raw exception details in WPF startup UI

## Problem

`WpfHost` currently logs startup failures correctly but also displays:

```csharp
ex.ToString()
```

directly to the user.

This exposes:

```text
stack traces
file paths
internal implementation details
assembly information
```

The full technical information belongs in logs.

---

## Required change

File:

```text
Core/MAP.C.Wpf/WpfHost.cs
```

Keep:

```csharp
logger.LogError(ex, "Startup failed");
```

Do not reduce logging detail.

Change the MessageBox to a user-facing message.

Example concept:

```text
MAP could not start.

Please review the application logs or contact support.
```

Include the current SessionId if useful:

```text
Session: {DiagnosticContext.SessionId}
```

Do not show:

```text
ex.ToString()
stack trace
raw inner exception
```

Do not create a generic error-dialog framework.

Do not localize this as part of this story unless existing localization can be used without complicating startup.

---

## Dispatcher exception policy

Do not modify the current corrected behavior:

```csharp
e.Handled = false;
```

Unknown Dispatcher exceptions must still be allowed to fail normally.

Do not restore `IsRecoverableException`.

---

## Acceptance Criteria

Startup failure:

```text
full exception in logs
short safe message in MessageBox
SessionId available for diagnostics
process shuts down
```

---

# STORY 4 — Make Shared the canonical source for Inter assets

## Problem

Inter assets currently exist as physical duplicates.

CSS:

```text
Shared/Styles/inter.css
MAP.H.Web/wwwroot/css/inter.css
MAP.H.Desktop/wwwroot/css/inter.css
```

Fonts also exist physically inside Web while Shared already contains canonical font files.

This creates unnecessary duplication and drift risk.

---

# Canonical ownership

The only canonical Inter sources should become:

```text
Shared/Styles/inter.css

Shared/Fonts/Inter/
├── Inter-latin.woff2
├── Inter-latin-ext.woff2
└── Inter-vietnamese.woff2
```

---

# Desktop

Update:

```text
MAP.H.Desktop/MAP.H.Desktop.csproj
```

Desktop already consumes Shared font files.

Also consume:

```text
Shared/Styles/inter.css
```

as:

```text
wwwroot/css/inter.css
```

Example concept:

```xml
<None Include="..\Shared\Styles\inter.css"
      Link="wwwroot\css\inter.css"
      CopyToOutputDirectory="PreserveNewest"
      CopyToPublishDirectory="PreserveNewest" />
```

Then remove the physical duplicate:

```text
MAP.H.Desktop/wwwroot/css/inter.css
```

Do not change:

```text
MAP.H.Desktop/wwwroot/css/app.css
```

---

# Web

Expose the Shared Inter assets through the existing static web asset mechanism.

Extend or add an appropriate static asset target.

Expose:

```text
Shared/Styles/inter.css
→ css/inter.css
```

and:

```text
Shared/Fonts/Inter/Inter-latin.woff2
→ fonts/inter/Inter-latin.woff2

Shared/Fonts/Inter/Inter-latin-ext.woff2
→ fonts/inter/Inter-latin-ext.woff2

Shared/Fonts/Inter/Inter-vietnamese.woff2
→ fonts/inter/Inter-vietnamese.woff2
```

Do not create new canonical copies.

After verification, delete:

```text
MAP.H.Web/wwwroot/css/inter.css

MAP.H.Web/wwwroot/fonts/inter/Inter-latin.woff2
MAP.H.Web/wwwroot/fonts/inter/Inter-latin-ext.woff2
MAP.H.Web/wwwroot/fonts/inter/Inter-vietnamese.woff2
```

---

## Keep app.css host-specific

Do NOT merge:

```text
MAP.H.Web/wwwroot/css/app.css
MAP.H.Desktop/wwwroot/css/app.css
```

They may remain separate.

Do not turn Shared into a generic CSS framework.

---

## Verify CSS path behavior

`inter.css` contains relative URLs:

```css
url("../fonts/inter/...")
```

Therefore both hosts must expose:

```text
/css/inter.css
/fonts/inter/*
```

at matching relative public paths.

Do not modify the CSS paths unless necessary.

---

## Acceptance Criteria

Repository contains only one canonical:

```text
Shared/Styles/inter.css
```

and one canonical set of Inter fonts under:

```text
Shared/Fonts/Inter/
```

At runtime:

```text
Web:
GET /css/inter.css → success
GET /fonts/inter/Inter-vietnamese.woff2 → success

Desktop:
wwwroot/css/inter.css available
wwwroot/fonts/inter/... available
```

Vietnamese text must render using Inter without browser font fallback caused by missing font files.

---

# STORY 5 — Minor cleanup only if directly touched

Do not perform a broad cleanup pass.

Two small items may be fixed if the related files are already being edited.

## WpfHost signature

If repository search confirms there is no caller using:

```csharp
configureUi
```

change:

```csharp
WpfHost.Run(
    Application application,
    Action<IServiceCollection>? configureUi = null)
```

to:

```csharp
WpfHost.Run(Application application)
```

Remove:

```csharp
configureUi?.Invoke(services);
```

Do this only if there is no real usage.

Do not replace it with another hook.

---

## Desktop PDB comment

Current behavior/comment is inconsistent.

Either make copying truly Debug-only:

```xml
Condition="'$(Configuration)' == 'Debug' ..."
```

or correct the comment.

Prefer Debug-only if there is no production requirement for Module PDBs.

This item is optional and must not delay the four primary fixes.

---

# DO NOT CHANGE

Do not modify:

```text
MainLayout navigation model
IPageNavigator
IModuleLoader contract
ModuleErrorBoundary
module folder architecture
module manifest behavior
Web Router — already removed
Desktop Router — already removed
WasmHost architecture
WpfHost ownership of MainLayout
MAP.H.Web thin Program.cs
MAP.H.Desktop thin App.xaml.cs
```

Do not introduce:

```text
AssemblyLoadContext
MediatR
CQRS
event bus
service locator
plugin manager
module lifecycle system
shared WPF/Wasm ModuleLoader base class
generic PlatformHost
new asset project
```

---

# EXPLICITLY DEFERRED

Do not address these now:

```text
DisplayInfo localization
DisplayHelper "Display / Primary" text responsibility
Wasm "Browser" display label
logging subsystem redesign
Web/Desktop app.css unification
build/deploy scripts
```

Record them only if relevant.

---

# REQUIRED VERIFICATION

## Build

Build at least:

```text
MAP.C.Wasm
MAP.C.Wpf
MAP.H.Web
MAP.H.Desktop
```

Run existing tests.

Do not modify build/deploy scripts.

---

## Web smoke test

Verify:

```text
startup
Tailwind styling
Inter font
Radzen
Header
Menu
default page
System Config
System Logs
module navigation
back navigation
language switching
```

---

## Desktop smoke test

Verify the same shell behaviors.

---

## Failure test — DB

Web and Desktop:

```text
missing db-api.json
invalid db-api.json
```

Expected:

```text
Shell starts
System pages remain available
DB operation throws clear controlled exception
```

---

## Failure test — Module

Test:

```text
missing module DLL
invalid component type
module render failure
```

Shell must remain usable according to existing fault-isolation rules.

---

## Concurrency test

Rapidly navigate between two pages from the same unloaded Module.

Verify:

```text
one assembly load
one localization initialization
loading indicator correct
no duplicate initialization
```

Repeat after forcing one failed load, then retry.

---

## Asset test

Web:

```text
/css/tailwind.css
/css/inter.css
/fonts/inter/Inter-latin.woff2
/fonts/inter/Inter-latin-ext.woff2
/fonts/inter/Inter-vietnamese.woff2
```

must load successfully.

Desktop must expose equivalent files under its BlazorWebView `wwwroot`.

---

# SUGGESTED COMMITS

Prefer small logical commits:

```text
fix(runtime): make fallback database clients fail clearly

fix(modules): deduplicate concurrent assembly loads

fix(wpf): hide technical details from startup error dialog

refactor(assets): use shared Inter assets across web and desktop
```

Optional:

```text
chore(wpf): remove unused host configuration hook
```

Do not combine everything into one commit.

---

# FINAL REPORT

At completion provide:

## Changed

List changed/deleted files grouped by:

```text
MAP.C.Wasm
MAP.C.Wpf
MAP.H.Web
MAP.H.Desktop
Shared
```

## DB fallback behavior

Explain the final behavior when `db-api.json` cannot be loaded.

## Module concurrency

Explain how duplicate in-flight assembly loads are prevented and how failures remain retryable.

## Static assets

Report the canonical source of truth for:

```text
Tailwind CSS
Inter CSS
Inter fonts
Web app.css
Desktop app.css
```

## Verification

Report actual results for:

```text
Web startup
Desktop startup
DB failure test
Module failure test
rapid navigation test
Inter/Tailwind asset test
existing automated tests
```

## Remaining Issues

Only include concrete issues discovered during implementation.

Do not propose further architecture redesign unless a real blocker was found.