# MAP HOST + DI REGISTRATION CLEANUP PLAN

## 0. Baseline

Review từ current repository state.

Current known baseline:

```text
f9e28667d65021b51599f5d6a7efc14c63cc8b99
```

Mục tiêu phase này:

```text
Host càng mỏng càng tốt
Platform runtime nằm đúng C.Wpf / C.Wasm
DI registration rõ ràng
Không duplicate initialization
Không thay đổi runtime behavior
```

Đây là cleanup/refactor phase.

Không redesign kiến trúc.

Không commit/push/open PR.

---

# 1. Architecture that MUST remain

Giữ nguyên:

```text
MAP.C.Contract
    stable contracts

MAP.C.Runtime
    platform-independent runtime

MAP.C.UI
    shared UI framework

MAP.C.Wpf
    Desktop platform runtime

MAP.C.Wasm
    Web platform runtime

MAP.H.Desktop
    Desktop bootstrap

MAP.H.Web
    Web bootstrap

Modules/*
    business/developer modules
```

Target:

```text
MAP.H.Desktop
    ↓
MAP.C.Wpf
    ↓
Runtime / UI / Contract
```

và:

```text
MAP.H.Web
    ↓
MAP.C.Wasm
    ↓
Runtime / UI / Contract
```

Host không được trở thành nơi chứa platform runtime.

---

# 2. Scope

Review và cleanup:

```text
MAP.H.Desktop
MAP.H.Web

Core/MAP.C.Wpf
Core/MAP.C.Wasm
```

Tập trung:

```text
bootstrap
DI registration
startup
lifecycle
duplicated registration
duplicated initialization
service lifetime
platform ownership
```

Có thể đọc `MAP.C.Runtime` / `MAP.C.UI` để xác định ownership, nhưng không cleanup rộng hai project này nếu không cần thiết.

---

# 3. Explicitly OUT OF SCOPE

Không sửa:

```text
deploy.ps1
deploy-all.ps1

Web Modules sync
MAP.H.Web/Modules strategy

Module fault-isolation
PageNavigator
ModuleErrorBoundary
ModuleErrorNotifier
ModuleErrorId

HeaderKind
HeaderStart
HeaderCenter
HeaderEnd

business Modules

config/log location
```

Không chuyển config/log sang `%LocalAppData%`.

Không thêm:

```text
MediatR
CQRS
EventBus
ServiceLocator
Result framework
Repository pattern
new factory framework
new startup framework
```

Không thêm NuGet nếu không thực sự bắt buộc.

---

# WP01 — Inventory current startup flow

Agent phải đọc toàn bộ startup path trước khi sửa.

Desktop:

```text
MAP.H.Desktop
    ↓
App startup
    ↓
MAP.C.Wpf registration/runtime
    ↓
MainWindow / WpfHost / BlazorWebView
```

Web:

```text
MAP.H.Web
    ↓
Program
    ↓
MAP.C.Wasm registration/runtime
    ↓
Blazor WebAssembly startup
```

Tạo internal review notes, không cần commit plan file.

Phân loại từng initialization thành:

```text
HOST_BOOTSTRAP
PLATFORM_RUNTIME
COMMON_RUNTIME
UI_REGISTRATION
APP_SPECIFIC
DUPLICATE
```

Không sửa trước khi xác định ownership.

---

# WP02 — Thin MAP.H.Desktop

## Goal

`MAP.H.Desktop` chỉ nên:

```text
create application/host
provide executable-specific paths/settings if necessary
call MAP.C.Wpf registration/startup
run application
```

Không nên chứa:

```text
WPF runtime implementation
module loader implementation
logging implementation
navigation implementation
localization implementation
complex service registration
UI framework composition
```

---

## Review

Inspect:

```text
MAP.H.Desktop/App.xaml
MAP.H.Desktop/App.xaml.cs
MAP.H.Desktop/*.csproj
other startup/bootstrap files
```

For every registration in Host:

```csharp
services.AddSomething(...)
```

ask:

```text
Is this Desktop executable bootstrap?
or
Is this reusable WPF platform runtime?
```

Nếu là reusable WPF runtime:

```text
move ownership to MAP.C.Wpf
```

Không chuyển `WpfHost`, `MainWindow` hoặc lifecycle từ `C.Wpf` sang Host.

Direction phải là:

```text
Host → C.Wpf
```

không phải ngược lại.

---

# WP03 — Thin MAP.H.Web

## Goal

`MAP.H.Web` chỉ nên:

```text
create WebAssemblyHostBuilder
provide web host environment
call MAP.C.Wasm registration/startup
run application
```

Không nên chứa reusable:

```text
Wasm platform services
logging setup logic
module loading logic
localization loading
navigation wiring
UI service setup
```

Những phần reusable của Web runtime thuộc:

```text
MAP.C.Wasm
```

---

## Critical rule

KHÔNG xóa hoặc redesign:

```text
MAP.H.Web/Modules
Web Modules copy/sync
lazy DLL loading
```

Developer Kit sau này vẫn phải có khả năng:

```text
build Module independently
→ copy DLL
→ Web host consumes DLL
```

ProjectReference hiện tại không phải lý do để xóa cơ chế này.

---

# WP04 — Consolidate platform DI registration

Review existing methods như:

```text
AddWpf(...)
AddWasm(...)
```

Target là mỗi platform có một entry point rõ ràng.

Conceptually:

```csharp
services.AddMapWpf(...);
```

hoặc giữ tên hiện tại nếu đã ổn.

Và:

```csharp
services.AddMapWasm(...);
```

Không rename public API chỉ để đẹp.

Ưu tiên giữ tên đang tồn tại nếu không có vấn đề.

---

## Platform registration ownership

### MAP.C.Wpf should own

Ví dụ:

```text
Desktop IModuleLoader
Desktop IAppConfigService
Desktop ILogStore
Desktop platform capabilities
Desktop-specific runtime services
Desktop implementation of platform contracts
```

### MAP.C.Wasm should own

Ví dụ:

```text
Wasm IModuleLoader
Wasm IAppConfigService
IndexedDB/Web ILogStore
Wasm platform capabilities
Wasm-specific runtime services
```

---

# WP05 — Shared service registration

Tìm service giống nhau đang được đăng ký cả Desktop và Web.

Ví dụ conceptual:

```text
PageNavigator
MenuService
ModuleErrorNotifier
shared UI state
shared runtime services
```

Nếu service thực sự platform-independent, xem registration nên nằm ở:

```text
MAP.C.Runtime
```

hoặc:

```text
MAP.C.UI
```

Nhưng chỉ move nếu:

```text
- ownership rõ
- giảm duplicate thật
- không tạo registration abstraction phức tạp
```

Nếu chỉ có 1–2 dòng giống nhau nhưng move làm code khó hiểu hơn:

```text
KEEP AS IS
```

Không DRY bằng mọi giá.

---

# WP06 — Detect duplicate registrations

Search toàn solution cho:

```text
AddScoped<
AddSingleton<
AddTransient<
TryAdd
AddRadzenComponents
AddLogging
ILoggerProvider
IPageNavigator
IModuleLoader
IAppConfigService
ILanguageService
IPlatformCapabilities
IPageHeaderState
IUiStateService
ModuleErrorNotifier
```

Detect cases:

```text
same service registered in Host + C.Wpf
same service registered in Host + C.Wasm
same service registered twice in same pipeline
implementation overwritten by later registration
different lifetime across Desktop/Web without reason
```

For each duplicate:

```text
KEEP
REMOVE
MOVE
```

và phải có lý do.

---

# WP07 — Review service lifetimes

Không đổi lifetime theo cảm tính.

Review especially:

```text
Singleton
Scoped
Transient
```

Questions:

```text
Does service hold per-app state?
Does it subscribe to events?
Does it contain navigation state?
Does it wrap platform storage?
Does it need disposal?
Does Blazor WebAssembly treat Scoped effectively app-scoped?
```

Focus services:

```text
IPageNavigator
IModuleLoader
IAppConfigService
ILanguageService
IPageHeaderState
IUiStateService
ModuleErrorNotifier
ILogStore
platform capabilities
```

Không đổi lifetime nếu behavior hiện tại đúng và không có defect.

Cleanup ≠ lifetime redesign.

---

# WP08 — Radzen registration ownership

Review:

```text
AddRadzenComponents()
```

Check Desktop and Web.

Goal:

```text
Radzen services registered exactly where required
not accidentally duplicated
```

Do not remove Radzen registrations required by:

```text
NotificationService
DialogService
TooltipService
ContextMenuService
```

Particularly preserve:

```text
ModuleErrorNotifier
→ NotificationService
```

Fault notification must continue working on both platforms.

---

# WP09 — Logging registration ownership

Review registration path for:

```text
LogStoreLoggerProvider
ILogStore
FileLogStore
IndexedDbLogStore
logging filters
```

Expected ownership:

```text
common logger provider
    → Runtime

platform storage
    → C.Wpf / C.Wasm
```

Host should not manually duplicate logger provider setup if platform Core can own it.

Must preserve:

```text
logger failures do not crash app
full exception logs
ErrorId correlation
SessionId
OperationId
```

Do not redesign logging format in this phase.

---

# WP10 — Configuration registration ownership

Review:

```text
IAppConfigService
```

Expected:

```text
Desktop implementation
    MAP.C.Wpf

Web implementation
    MAP.C.Wasm
```

Host should not contain configuration implementation logic.

Preserve current accepted behavior:

```text
Desktop config stays beside executable
Desktop logs stay beside executable
```

No `%LocalAppData%`.

---

# WP11 — Platform capabilities

Preserve:

```text
IPlatformCapabilities
```

Expected:

```text
WPF implementation
    SupportsFullscreen etc. according to Desktop

WASM implementation
    unsupported capabilities false
```

Do not remove contract just because values are simple.

`MAP.M.System` depends on it.

---

# WP12 — GetSystemInfo

Preserve:

```csharp
IAppConfigService.GetSystemInfo()
```

Do not remove because implementations currently appear trivial.

Developer Kit Modules depend only on Contract/UI and may use this API later.

---

# WP13 — Startup side effects

Review startup code for side effects such as:

```text
loading config
loading menus
initializing log store
loading localization
registering JS callbacks
creating directories
loading modules
```

For each, ensure execution occurs:

```text
once
at correct lifecycle stage
in correct layer
```

Look specifically for:

```text
same initialization executed Host + Core
initialization performed before required services exist
fire-and-forget startup tasks
async void startup paths
```

Avoid introducing complicated startup orchestration.

---

# WP14 — Async startup correctness

Search:

```text
async void
_ = SomeAsync()
Task.Run(...)
.GetAwaiter().GetResult()
.Result
.Wait()
```

Only within Host / C.Wpf / C.Wasm scope.

Evaluate individually.

Do not blindly replace synchronous WPF startup where framework requires it.

But avoid unobserved startup exceptions.

No `async void` except genuine framework event signature requirement.

---

# WP15 — Dependency direction

Check project references.

Expected:

```text
MAP.H.Desktop
    → MAP.C.Wpf

MAP.H.Web
    → MAP.C.Wasm
```

Platform Core can reference appropriate common projects:

```text
Contract
Runtime
UI
```

Forbidden direction:

```text
MAP.C.Wpf → MAP.H.Desktop
MAP.C.Wasm → MAP.H.Web

Contract → platform
Runtime → Host
UI → Host
```

Modules must not gain new platform references.

---

# WP16 — Host-specific code that SHOULD remain

Do not move code merely to make Host tiny.

Host-specific code may legitimately remain when it depends on executable identity.

Examples:

```text
application executable startup
host environment
WebAssemblyHostBuilder creation
WPF Application entry
process-level bootstrap
host project static assets
Web wwwroot host assets
```

Rule:

```text
Thin Host ≠ Empty Host
```

---

# WP17 — Remove dead startup code

After ownership cleanup, remove only verified dead code:

```text
unused using
unused injected service
unused helper
duplicate registration
unused startup variable
obsolete commented-out initialization
```

Do not remove future public extension contracts.

Do not remove code just because current demo Modules do not use it.

---

# WP18 — Avoid unnecessary new abstractions

Do NOT create things like:

```text
IPlatformBootstrapper
IHostInitializer
IStartupCoordinator
IRegistrationProvider
ServiceRegistrationManager
ApplicationStartupPipeline
BootstrapFactory
```

unless an existing real problem absolutely requires one.

Preferred:

```text
simple extension methods
direct registrations
clear Program/App startup
```

---

# WP19 — Expected target shape

Desktop conceptual target:

```text
MAP.H.Desktop/App
{
    create/start desktop app
    call C.Wpf platform setup
}
```

```text
MAP.C.Wpf
{
    AddWpf(...)
    WpfHost
    MainWindow
    desktop lifecycle
    desktop module loader
    desktop config
    desktop storage/logging
    platform capabilities
}
```

Web conceptual target:

```text
MAP.H.Web/Program
{
    create WebAssemblyHostBuilder
    call C.Wasm platform setup
    RunAsync
}
```

```text
MAP.C.Wasm
{
    AddWasm(...)
    web module loader
    web config/storage
    web logging
    platform capabilities
    reusable Wasm runtime
}
```

No need for exact symmetry if platform requirements differ.

---

# WP20 — Regression checks

After cleanup verify:

## Desktop

```text
starts normally
config loads
menu loads
module loads
Radzen dialogs work
Radzen notifications work
navigation works
fault isolation works
logging works
```

## Web

```text
starts normally
config loads
menu loads
module DLL lazy loading works
module localization works
Radzen works
navigation works
fault isolation works
logging works
```

---

# WP21 — Developer Kit compatibility

Confirm Modules still only need:

```text
MAP.C.Contract
MAP.C.UI
Radzen.Blazor
```

No Module should require:

```text
MAP.C.Wpf
MAP.C.Wasm
MAP.H.Desktop
MAP.H.Web
```

Search project references after cleanup.

---

# WP22 — Build verification

Run:

```powershell
dotnet restore MAP.slnx
```

```powershell
dotnet build MAP.slnx -c Debug --no-restore
```

```powershell
dotnet build MAP.slnx -c Release --no-restore
```

```powershell
dotnet test Tests/MAP.C.Runtime.Tests/MAP.C.Runtime.Tests.csproj -c Release
```

Then:

```powershell
dotnet publish MAP.H.Desktop/MAP.H.Desktop.csproj -c Release
```

```powershell
dotnet publish MAP.H.Web/MAP.H.Web.csproj -c Release
```

If environment supports:

```powershell
.\build.ps1
```

```powershell
.\build-all.ps1
```

Do not run deploy.

If a command cannot be run:

```text
NOT RUN
```

Do not report PASS without execution.

---

# WP23 — Manual smoke test

If runtime environment is available:

### Desktop

```text
launch
open multiple modules
same-page parameter navigation
back navigation
System config
System logs
trigger safe module error if test mechanism exists
```

### Web

```text
launch
open lazy-loaded module
switch modules
back navigation
localization
fault notification
```

Do not add permanent faulty test Module to production repository.

---

# WP24 — Git diff review

Before final report:

```text
git diff
```

Review every changed file.

Ensure there are no accidental changes to:

```text
deploy
Modules
navigation
fault isolation
Header contract
Web Modules sync
config/log location
```

No generated build output should be staged.

---

# Definition of Done

```text
[ ] Desktop Host contains bootstrap only
[ ] Web Host contains bootstrap only
[ ] reusable WPF runtime remains in C.Wpf
[ ] reusable WASM runtime remains in C.Wasm

[ ] duplicate DI registrations reviewed
[ ] unnecessary duplicates removed
[ ] service lifetimes remain intentional
[ ] Radzen registered correctly on both platforms
[ ] NotificationService still available
[ ] logging ownership remains correct
[ ] config ownership remains correct
[ ] IPlatformCapabilities preserved
[ ] GetSystemInfo preserved

[ ] no C.Wpf → Host dependency
[ ] no C.Wasm → Host dependency
[ ] Modules gain no platform dependency

[ ] Web Modules sync preserved
[ ] Header API preserved
[ ] fault-isolation code unchanged
[ ] deploy unchanged
[ ] config/log executable-adjacent behavior unchanged

[ ] no unnecessary abstraction added
[ ] no unnecessary NuGet added

[ ] Debug build PASS
[ ] Release build PASS
[ ] Runtime tests PASS
[ ] Desktop publish PASS or NOT RUN
[ ] Web publish PASS or NOT RUN
[ ] manual Desktop smoke PASS or NOT RUN
[ ] manual Web smoke PASS or NOT RUN

[ ] no commit
[ ] no push
[ ] no PR
```

---

# Agent final report format

## 1. Architecture findings

```text
Desktop Host:
Web Host:
C.Wpf:
C.Wasm:
```

Describe what was misplaced or duplicated.

## 2. Changed files

```text
Modified:
Deleted:
Added:
```

For each changed file:

```text
WHY
WHAT
BEHAVIOR IMPACT
```

## 3. DI changes

Report:

```text
Service
Before registration
After registration
Lifetime
Reason
```

Do not list unchanged services unless important.

## 4. Removed duplication

```text
Registration:
Initialization:
Dead code:
```

## 5. Architecture confirmation

Explicitly confirm:

```text
Host → platform Core direction preserved
C.Wpf owns Desktop runtime
C.Wasm owns Web runtime
Web Modules sync unchanged
Module dependency rules unchanged
Header API unchanged
fault isolation unchanged
deploy unchanged
```

## 6. Verification

```text
dotnet restore             PASS / FAIL / NOT RUN
Debug build                PASS / FAIL / NOT RUN
Release build              PASS / FAIL / NOT RUN
Runtime tests              PASS / FAIL / NOT RUN
Desktop publish            PASS / FAIL / NOT RUN
Web publish                PASS / FAIL / NOT RUN
Desktop smoke              PASS / FAIL / NOT RUN
Web smoke                  PASS / FAIL / NOT RUN
```

Include actual failures if any.

## 7. Remaining findings

Classify:

```text
P1 blocking
P2 should fix
P3 optional cleanup
```

Do not silently fix unrelated findings.

If unrelated cleanup is discovered, report it for the next phase instead of expanding current scope.

---

# Final principle

This phase is successful when:

```text
Host becomes easier to understand
DI registration ownership becomes obvious
runtime behavior stays the same
```

Not when the code is made maximally abstract or maximally DRY.