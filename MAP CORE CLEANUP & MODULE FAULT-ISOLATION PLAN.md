# MAP CORE CLEANUP & MODULE FAULT-ISOLATION PLAN

## 0. Mục tiêu

Cleanup MAP theo nguyên tắc:

- Đơn giản.
- Ít code trùng lặp.
- Không tạo abstraction chỉ để “đẹp kiến trúc”.
- Core cung cấp đầy đủ runtime.
- Host càng mỏng càng tốt.
- Developer Kit chủ yếu chỉ làm việc với Module.
- Module lỗi không được làm sập shell/application trong các lỗi managed thông thường.
- Lỗi Module luôn đi qua logging path.
- Việc có hiển thị thông báo lỗi cho user hay không được điều khiển bằng config.
- Thông báo lỗi sử dụng **Radzen Notification**, không hiển thị exception trực tiếp trên page.

---

# 1. Kiến trúc phải giữ nguyên

Không gộp project.

```text
Core/
├─ MAP.C.Contract
├─ MAP.C.Runtime
├─ MAP.C.UI
├─ MAP.C.Wpf
└─ MAP.C.Wasm

MAP.H.Desktop
MAP.H.Web

Modules/
└─ MAP.M.*
```

Trách nhiệm:

```text
MAP.C.Contract
    Contract ổn định mà Module có thể sử dụng.

MAP.C.Runtime
    Logic chung không phụ thuộc Desktop/Web.

MAP.C.UI
    UI framework/component chung cho Module.

MAP.C.Wpf
    Toàn bộ implementation/runtime chỉ dành cho Desktop.

MAP.C.Wasm
    Toàn bộ implementation/runtime chỉ dành cho Web.

MAP.H.Desktop
    Bootstrap/run Desktop.

MAP.H.Web
    Bootstrap/run Web + đóng gói Web module.

Modules
    Business/module code.
```

Developer Module lý tưởng chỉ cần:

```text
MAP.C.Contract
MAP.C.UI
Radzen.Blazor
```

Module không được phụ thuộc:

```text
MAP.C.Wpf
MAP.C.Wasm
MAP.H.Desktop
MAP.H.Web
```

---

# 2. Những phần KHÔNG được thay đổi

## 2.1 Header API

Giữ nguyên:

```text
HeaderKind
HeaderStart
HeaderCenter
HeaderEnd
```

Không cleanup/xóa dù hiện tại có phần chưa sử dụng.

Đây là extension API dành cho Module tương lai.

---

## 2.2 Web Modules sync

Giữ nguyên ý tưởng:

```text
Developer Kit
    ↓
Module project
    ↓
build riêng
    ↓
MAP.M.X.dll
    ↓
copy về source chính
    ↓
MAP.H.Web/Modules/
    ↓
Web build
    ↓
LazyAssemblyLoader
```

Không xóa `MAP.H.Web/Modules`.

Không refactor theo hướng Web bắt buộc phải có source project của Module.

Current solution có Module source chung chỉ phục vụ development hiện tại.

Việc chuyển hoàn toàn sang DLL-only build là phase Developer Kit sau này, không thuộc cleanup hiện tại.

---

## 2.3 C.Wpf và C.Wasm

Không chuyển:

```text
WpfHost
WpfServices
MainWindow
WPF runtime
```

ra `MAP.H.Desktop`.

`C.Wpf` phải chứa toàn bộ Desktop platform runtime.

Tương tự `C.Wasm` phải chứa toàn bộ Web platform runtime.

Mục tiêu:

```text
Host = bootstrap
Core platform = runtime thật
```

---

## 2.4 Không thay đổi deploy

Không sửa:

```text
deploy.ps1
deploy-all.ps1
```

trong cleanup này trừ khi build bị ảnh hưởng trực tiếp bởi thay đổi cleanup.

Không giải quyết Web deploy path trong plan này.

---

## 2.5 Giữ config/log cạnh executable

Không chuyển config/log sang `%LocalAppData%`.

Giữ behavior hiện tại.

---

## 2.6 Không thêm framework kiến trúc

Không thêm:

```text
MediatR
CQRS
EventBus
Repository pattern
Result<T> framework
global message bus
module service locator
new architectural framework
```

Không thêm NuGet nếu không thật sự cần thiết.

---

# 3. Module Fault Isolation — nguyên tắc bắt buộc

Các lỗi managed thông thường của Module phải được cô lập.

Ví dụ:

```text
DLL không tồn tại
DLL load lỗi
component type sai
localization lỗi
constructor lỗi
OnInitialized lỗi
OnParametersSet lỗi
OnAfterRender lỗi
render lỗi
event handler lỗi
navigation tới module lỗi
```

Expected:

```text
Module lỗi
    ↓
Core bắt exception
    ↓
log lỗi
    ↓
Radzen Notification nếu config cho phép
    ↓
Shell vẫn chạy
    ↓
Header/Menu vẫn hoạt động
    ↓
user có thể mở chức năng khác
```

Không được:

```text
Module lỗi
    ↓
Desktop/Web application chết
```

Không thể cam kết cô lập các lỗi process-level như:

```text
StackOverflowException
OutOfMemoryException
AccessViolationException
Environment.FailFast()
native process corruption
OS/process termination
```

Không cố bắt các lỗi này chỉ để giữ process sống.

---

# PHASE 1 — LOW-RISK CLEANUP

# WP01 — Cleanup IModuleLoader contract

## Mục tiêu

Giảm API không cần thiết nhưng không ảnh hưởng Web Modules design.

Hiện:

```csharp
public interface IModuleLoader
{
    event Action<bool>? OnLoadingChanged;
    event Action<string>? OnError;

    Task<Type?> LoadComponentAsync(MenuItem menuItem);

    Type? GetCachedType(
        string assemblyName,
        string componentName);
}
```

Agent phải search toàn repository trước.

Nếu xác nhận không có consumer:

```text
OnError
GetCachedType
```

thì xóa khỏi public contract.

Cache nội bộ trong WPF/Wasm loader vẫn giữ.

---

## LoadComponentAsync

Nếu mọi implementation hiện:

```text
success → trả Type
failure → throw
```

thì đổi:

```csharp
Task<Type?>
```

thành:

```csharp
Task<Type>
```

PageNavigator không cần:

```csharp
?? throw ...
```

nữa.

Không thay đổi semantics của module cache/localization.

---

# WP02 — Gộp LoggerProvider

Hiện có:

```text
MAP.C.Wpf
    FileLoggerProvider

MAP.C.Wasm
    IndexedDbLoggerProvider
```

Hai implementation gần như giống nhau.

Khác biệt thực sự nằm ở:

```text
FileLogStore
IndexedDbLogStore
```

## Refactor

Tạo:

```text
MAP.C.Runtime/
└─ Logging/
   └─ LogStoreLoggerProvider.cs
```

Flow:

```text
ILogger
    ↓
LogStoreLoggerProvider
    ↓
ILogStore
    ├─ FileLogStore
    └─ IndexedDbLogStore
```

Xóa:

```text
FileLoggerProvider
IndexedDbLoggerProvider
```

sau khi platform registrations đã chuyển sang provider chung.

---

## Logging failure

Logger failure tuyệt đối không được làm application crash.

Pattern:

```text
try
    write persistent log
catch
    Debug/Console fallback
```

Không swallow exception mà hoàn toàn không có diagnostic fallback.

---

# WP03 — Cleanup MenuService duplication

Không tạo `BaseMenuService`.

Không tạo provider/factory/strategy pattern.

WPF và Wasm vẫn có MenuService riêng.

Platform-specific:

```text
WPF
    đọc page.json từ filesystem

Wasm
    đọc page.json bằng HTTP
```

Giữ riêng.

Logic chung hiện đang duplicate:

```text
resolve MenuSource
load DB menu
DB failure → fallback local
SystemMenus.EnsureRegistered
```

Extract thành helper nhỏ:

```text
MAP.C.Runtime/Menus/MenuConfigResolver.cs
```

Ví dụ responsibility:

```text
local PageConfig
    ↓
effective menu source
    ↓
optional database load
    ↓
DB lỗi → giữ local
    ↓
SystemMenus.EnsureRegistered
    ↓
return PageConfig
```

Sau refactor:

```text
Wpf.MenuService
    local file I/O
    + call MenuConfigResolver

Wasm.MenuService
    local HTTP I/O
    + call MenuConfigResolver
```

Không thay đổi behavior hiện tại.

---

# WP04 — Thống nhất WPF display handling

Hiện WPF có hai cơ chế:

```text
AppConfigService
    EnumDisplayDevices

DisplayHelper
    EnumDisplayMonitors
```

Không được có hai nguồn `DisplayIndex`.

## Refactor

`DisplayHelper` là source duy nhất:

```text
GetDisplays()
PositionOnDisplay()
FullscreenOnDisplay()
```

`AppConfigService` chỉ gọi:

```csharp
public IReadOnlyList<DisplayInfo> GetDisplays()
    => DisplayHelper.GetDisplays();
```

Xóa P/Invoke display duplication khỏi `AppConfigService`.

Expected:

```text
DisplayIndex user chọn
```

và:

```text
DisplayIndex dùng để position/fullscreen
```

phải sinh từ cùng một enumeration.

---

# PHASE 2 — MODULE ERROR SAFETY

Đây là phase quan trọng nhất.

# WP05 — Module error notification config

Thêm vào `AppConfig`:

```csharp
public bool ShowModuleErrorNotification { get; set; } = true;

public string? ModuleErrorMessage { get; set; }
```

Không thêm thêm nhiều setting.

Nếu `ModuleErrorMessage` null/empty:

```text
dùng localization fallback
```

Ví dụ:

```text
"Chức năng tạm thời không khả dụng."
```

---

## MAP.M.System / AppConfigPage

Thêm:

```text
Thông báo khi chức năng lỗi
[ ON/OFF ]

Nội dung thông báo
[ Chức năng tạm thời không khả dụng. ]
```

Khi OFF:

- có thể disable/hide textbox.
- không hiện Notification khi Module lỗi.

Quan trọng:

```text
ShowModuleErrorNotification
```

chỉ ảnh hưởng UI.

Không ảnh hưởng logging.

---

# WP06 — Central ModuleErrorNotifier

Không để mỗi component tự viết notification.

Tạo một implementation đơn giản trong:

```text
MAP.C.UI/Errors/ModuleErrorNotifier.cs
```

Không cần interface nếu hiện chỉ Core UI sử dụng.

Dependencies:

```text
NotificationService
IAppConfigService
ILanguageService
```

Responsibility duy nhất:

```text
Module error
    ↓
check ShowModuleErrorNotification
    ↓
resolve configured/localized message
    ↓
Radzen Notification
```

Không log exception tại đây.

Không chứa navigation logic.

Không chứa module loading logic.

---

## Notification

Dùng Radzen:

```text
NotificationSeverity.Error
```

Thông báo dạng:

```text
Lỗi chức năng

Chức năng tạm thời không khả dụng.
Mã lỗi: A83F28C1
```

Không hiển thị:

```text
exception.Message
stack trace
file path
connection information
InnerException
technical diagnostic
```

Notification duration dùng một giá trị cố định hợp lý trong Core.

Không thêm config cho duration.

Ví dụ:

```text
6000 ms
```

---

# WP07 — Notification outlet

Đảm bảo chỉ có một Notification outlet cho toàn shell.

`MainLayout` hoặc root shared UI phải chứa Radzen Notification component phù hợp với cách project hiện đang dùng Radzen.

Không đặt Notification component trong từng Module.

Không để mỗi Host tự tạo behavior khác nhau.

WPF và Web phải dùng cùng UI notification behavior.

---

# WP08 — Safe navigation / module-load failure

Module load lỗi xảy ra trước `DynamicComponent`.

Các lỗi gồm:

```text
assembly missing
assembly load failure
localization failure
component type missing
```

`PageNavigator` hiện đã có transactional behavior:

```text
load component trước
stack mutate sau
```

Giữ nguyên.

Nếu load fail:

```text
current page không được mất
navigation stack không được corrupt
```

---

## Core UI safe-open

Trong `MainLayout`, tạo một entry point duy nhất, ví dụ:

```text
OpenPageSafeAsync(...)
```

Responsibility:

```text
try
    Navigator.OpenAsync()
catch exception
    tạo ErrorId
    gọi ModuleErrorNotifier
    không rethrow ra UI event
```

PageNavigator vẫn chịu trách nhiệm logging navigation failure.

Không duplicate full LogError trong `MainLayout`.

---

## Tất cả navigation từ shell phải đi qua safe-open

Bao gồm:

```text
menu click
default page
system config startup
system logs shortcut
các shell navigation khác
```

Không copy/paste try/catch mỗi nơi.

---

# WP09 — Render/lifecycle ErrorBoundary

Load thành công chưa đủ.

Module vẫn có thể throw trong:

```text
constructor
OnInitialized
OnParametersSet
OnAfterRender
render tree
UI event
```

`DynamicComponent` phải được bao quanh bởi một ErrorBoundary chỉ dành cho Module.

Không wrap toàn application.

Target:

```text
MainLayout
    Header
    Menu
    PageContainer
        Module ErrorBoundary
            DynamicComponent
```

Nếu Module lỗi:

```text
DynamicComponent chết
```

nhưng:

```text
MainLayout sống
Header sống
Menu sống
RadzenNotification sống
```

---

## Custom Module ErrorBoundary

Tạo:

```text
MAP.C.UI/Errors/ModuleErrorBoundary
```

Có thể subclass/wrap Blazor ErrorBoundary.

Responsibility:

1. Catch descendant Module exception.
2. Tạo short `ErrorId`.
3. Log full exception.
4. Gọi `ModuleErrorNotifier`.
5. Không rethrow lỗi Module managed thông thường.
6. Không render raw error panel.

---

## ErrorContent

Không dùng panel:

```text
Đã xảy ra lỗi
exception.Message
Retry
Back
```

như hiện tại.

Sau khi Module lỗi, page content có thể:

```text
blank/minimal neutral content
```

nhưng thông báo lỗi chính phải là Radzen Notification.

Không render exception text.

User vẫn có Header/Menu để rời khỏi Module lỗi.

---

# WP10 — ErrorId

Tạo ErrorId ngắn cho mỗi Module failure.

Ví dụ:

```text
A83F28C1
```

Không cần service phức tạp.

Có thể:

```csharp
Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()
```

Log:

```text
ErrorId
SessionId
OperationId
PageId
Assembly
Component
```

Notification chỉ cần:

```text
message
ErrorId
```

Điều này giúp user báo:

```text
"Mã lỗi A83F28C1"
```

và developer tra log nhanh.

---

# WP11 — Logging ownership

Không log cùng exception full stack ở 3 layer.

## Module loading/navigation failure

Ownership:

```text
PageNavigator
```

Log:

```text
ErrorId nếu có
NavigationId
PageId
FromPageId
Assembly
Component
SessionId
OperationId
Duration
full exception
```

ModuleLoader chỉ log technical loading events nếu cần:

```text
start
cache hit
assembly path/name
duration
```

Tránh full `LogError(exception)` lặp thêm nếu PageNavigator chắc chắn sẽ log cùng lỗi.

Nếu ModuleLoader cần log vì nó có thể được gọi ngoài Navigator, cân nhắc ownership kỹ; không mechanically duplicate.

---

## Render/lifecycle failure

Ownership:

```text
ModuleErrorBoundary
```

Log full exception đúng một lần.

`ModuleErrorNotifier` không log.

---

## UI notification

Không log thêm lần nữa.

---

# WP12 — Logging reliability

Yêu cầu:

```text
mọi Module error path
→ phải gọi logging
```

Config Notification không được disable logging.

---

## WPF

Persistent store:

```text
log/yyyy-MM-dd.log
```

Nếu file logging thất bại:

```text
Debug fallback
```

Logging failure không được crash shell.

---

## Web

Persistent store:

```text
IndexedDB
```

Giữ JavaScript fallback:

```text
window.error
unhandledrejection
console.error
```

Nếu IndexedDB unavailable/quota failure:

```text
console diagnostic fallback
```

Không để IndexedDB failure làm Web application crash.

---

## Technical guarantee

Không hứa storage luôn ghi thành công trong các trường hợp:

```text
disk full
disk read-only
browser quota exceeded
IndexedDB disabled
process bị kill
OS crash
```

Yêu cầu chính:

```text
mọi error path đều thực hiện logging
+
persistent logging khi storage hoạt động
+
fallback diagnostic khi persistent storage lỗi
```

---

# WP13 — Module developer safety rules

Tạo tài liệu ngắn cho Developer Kit:

```text
MODULE_RULES.md
```

Rule:

### Allowed references

```text
MAP.C.Contract
MAP.C.UI
Radzen.Blazor
```

### Forbidden direct references

```text
MAP.C.Wpf
MAP.C.Wasm
MAP.H.Desktop
MAP.H.Web
```

---

## Async

Không dùng:

```csharp
async void
```

trừ API event bắt buộc.

Không:

```csharp
_ = SomeAsync();
```

nếu task có thể throw mà không được observe/log.

Preferred:

```csharp
await SomeAsync();
```

hoặc explicit safe wrapper.

---

## Process control

Module không được gọi trực tiếp:

```text
Environment.Exit
Environment.FailFast
Application.Current.Shutdown
Process.Kill
window.location...
```

Platform/process behavior phải qua Core contract nếu thực sự cần.

---

# PHASE 3 — STATIC ASSET CLEANUP

# WP14 — Loại duplicate CSS/font

Hiện có asset duplicate giữa:

```text
Shared
H.Web
H.Desktop
```

và Desktop còn lấy `tailwind.css` từ Web.

Không để:

```text
H.Desktop → H.Web
```

qua filesystem.

---

## Preferred target

UI assets dùng chung nên thuộc:

```text
MAP.C.UI/wwwroot/
```

Ví dụ:

```text
MAP.C.UI/
└─ wwwroot/
   ├─ css/
   │  ├─ inter.css
   │  ├─ app-common.css
   │  └─ tailwind.css
   │
   └─ fonts/
      └─ inter/
```

Host load qua static web assets:

```text
_content/MAP.C.UI/...
```

---

## Tailwind

`build-css.ps1` output về shared UI asset:

```text
Core/MAP.C.UI/wwwroot/css/tailwind.css
```

Không generate vào `MAP.H.Web`.

---

## app.css

Chỉ extract phần thực sự giống nhau.

Web-specific loading CSS vẫn có thể ở Web.

Desktop-specific host/splash CSS vẫn có thể ở Desktop.

Không cố gộp tất cả CSS thành một file.

---

# PHASE 4 — THIN HOSTS

# WP15 — Thin MAP.H.Desktop

Mục tiêu:

```text
MAP.H.Desktop
```

chỉ bootstrap/run.

Move registration chung hiện Host đang làm về `MAP.C.Wpf` nếu đó là Core runtime responsibility:

```text
IPageHeaderState
RadzenLocalizer
Radzen components
shared UI services
```

Target `App.xaml.cs` càng gần:

```csharp
public partial class App : Application
{
    public App()
    {
        WpfHost.Run(this, typeof(...));
    }
}
```

Không chuyển `MainWindow` ra khỏi C.Wpf.

Không chuyển Desktop runtime vào Host.

---

# WP16 — Thin MAP.H.Web

Hiện `Program.cs` register quá nhiều Core service.

Đưa Web runtime composition về:

```text
MAP.C.Wasm
```

Có thể dùng API kiểu:

```text
WasmHost.RunAsync(...)
```

hoặc:

```text
builder.AddMapWasmAsync(...)
```

Chọn giải pháp đơn giản nhất.

C.Wasm chịu trách nhiệm:

```text
logging
ILogStore
config
database client
localization
MenuService
ModuleLoader
PageNavigator
UI services
Radzen services
startup logging
config initialization
```

H.Web chỉ giữ:

```text
WebAssemblyHost
root component
host page
Web Modules packaging
page/db config packaging
Web-specific static host requirements
```

Target `Program.cs` chỉ còn bootstrap ngắn.

---

# WP17 — Router/template residue

Chỉ làm sau khi Thin Host đã pass regression.

Kiểm tra thực tế xem application có cần:

```text
Blazor URL Router
Home.razor
NotFound.razor
```

hay navigation thực tế hoàn toàn dùng:

```text
IPageNavigator
→ PageContainer
→ DynamicComponent
```

Nếu không có requirement:

```text
deep-link URL
browser history URL
direct route navigation
```

thì có thể xóa template routing dư.

Candidate:

```text
MAP.H.Desktop/DesktopApp.razor
MAP.H.Desktop/Pages/Home.razor
MAP.H.Desktop/Pages/NotFound.razor

MAP.H.Web/App.razor
MAP.H.Web/Pages/Home.razor
MAP.H.Web/Pages/NotFound.razor
```

Không xóa nếu runtime vẫn cần Router.

Thực hiện thành commit/work package riêng để dễ rollback.

---

# PHASE 5 — DEAD CODE SWEEP

# WP18 — Repository-wide dead code cleanup

Search toàn repository.

Candidate hiện tại:

```text
IModuleLoader.OnError
IModuleLoader.GetCachedType
unused MainLayout injections
unused using directives
duplicate CSS
duplicate fonts
zero-byte favicon
obsolete Router components
obsolete static assets
unused helper methods
```

Quy trình:

```text
search usage
    ↓
zero consumer
    ↓
xóa
    ↓
build/test
```

Không xóa dựa trên cảm giác.

---

## Explicit keep list

Không xóa:

```text
HeaderKind
HeaderStart
HeaderCenter
HeaderEnd

IPlatformCapabilities
IAppConfigService.GetSystemInfo

MAP.H.Web/Modules
Web Modules sync
```

---

# PHASE 6 — TEST MODULE FAULT ISOLATION

Không chấp nhận chỉ `dotnet build` pass.

Phải fault-injection test.

Không commit temporary faulty module/component vào production source.

---

# TEST01 — Assembly missing

Tạo menu trỏ tới DLL không tồn tại.

Expected:

```text
navigation fail
current page giữ nguyên
shell sống
Header/Menu sống
Notification hiện nếu config ON
Notification không hiện nếu config OFF
log có full exception
```

---

# TEST02 — Component type missing

DLL tồn tại nhưng component name sai.

Expected giống TEST01.

---

# TEST03 — Localization failure

Cố tình làm localization load fail.

Expected:

```text
shell sống
navigation stack không đổi
assembly không được commit vào initialized cache
retry sau khi sửa lỗi có thể load lại
Notification theo config
log đầy đủ
```

---

# TEST04 — Constructor failure

Test component:

```csharp
throw new InvalidOperationException("FAULT TEST");
```

trong constructor.

Expected:

```text
ErrorBoundary cô lập lỗi
Header sống
Menu sống
Notification Error
log có ErrorId
raw exception không xuất hiện UI
có thể mở module khác
```

---

# TEST05 — OnInitialized failure

Cố tình throw trong:

```text
OnInitialized
OnInitializedAsync
```

Expected giống TEST04.

---

# TEST06 — Parameters/render failure

Throw trong:

```text
OnParametersSet
render logic
```

Expected giống TEST04.

---

# TEST07 — AfterRender failure

Throw trong:

```text
OnAfterRender
OnAfterRenderAsync
```

Expected:

```text
shell không chết
Notification
log
module khác vẫn mở được
```

---

# TEST08 — Button/event handler failure

Button trong Module:

```csharp
private void ThrowTest()
{
    throw new InvalidOperationException("FAULT TEST");
}
```

Expected:

```text
exception được containment mechanism xử lý
shell vẫn hoạt động
Notification
log
```

Nếu Blazor ErrorBoundary behavior không cover một dạng event cụ thể:

- xử lý tại Core component boundary phù hợp,
- không tạo global catch-all phá debugging,
- document limitation nếu framework không thể isolate an toàn.

---

# TEST09 — Notification config ON

Config:

```json
{
  "showModuleErrorNotification": true,
  "moduleErrorMessage": "Chức năng hiện không khả dụng."
}
```

Expected Notification:

```text
Lỗi chức năng

Chức năng hiện không khả dụng.
Mã lỗi: XXXXXXXX
```

Không có raw exception.

Log có full exception.

---

# TEST10 — Notification config OFF

Config:

```json
{
  "showModuleErrorNotification": false
}
```

Expected:

```text
không Notification
shell vẫn chạy
log vẫn ghi
```

---

# TEST11 — Logging storage failure

WPF:

```text
simulate log folder write failure nếu có thể an toàn
```

Web:

```text
simulate IndexedDB failure/quota/unavailable nếu có thể
```

Expected:

```text
application không crash
fallback diagnostic xuất hiện
```

---

# 4. Build / regression verification

Sau mỗi WP hoặc nhóm WP:

```powershell
dotnet restore MAP.slnx

dotnet build MAP.slnx -c Debug --no-restore

dotnet build MAP.slnx -c Release --no-restore

dotnet test Tests/MAP.C.Runtime.Tests/MAP.C.Runtime.Tests.csproj `
    -c Release
```

Sau thay đổi WPF/Wasm/UI assets/Hosts:

```powershell
dotnet publish MAP.H.Desktop/MAP.H.Desktop.csproj `
    -c Release

dotnet publish MAP.H.Web/MAP.H.Web.csproj `
    -c Release

.\build.ps1

.\build-all.ps1
```

Không chạy production deploy.

---

# 5. Thứ tự thực hiện bắt buộc

Không làm tất cả trong một thay đổi lớn.

## Step 1 — Core cleanup

Thực hiện:

```text
WP01 IModuleLoader
WP02 LoggerProvider
WP03 MenuService duplication
WP04 WPF Display
```

Build/test.

---

## Step 2 — Module safety

Thực hiện:

```text
WP05 Error config
WP06 ModuleErrorNotifier
WP07 Notification outlet
WP08 Safe navigation
WP09 ErrorBoundary
WP10 ErrorId
WP11 Logging ownership
WP12 Logging reliability
WP13 Module rules
```

Đây là checkpoint quan trọng nhất.

Chạy toàn bộ Module fault tests trước khi đi tiếp.

---

## Step 3 — UI asset cleanup

Thực hiện:

```text
WP14 static asset cleanup
```

Visual regression cả Desktop/Web.

---

## Step 4 — Thin Hosts

Thực hiện:

```text
WP15 H.Desktop
WP16 H.Web
```

Build/publish Desktop và Web.

---

## Step 5 — Optional residue cleanup

Thực hiện:

```text
WP17 Router/template cleanup
WP18 dead-code sweep
```

Chỉ sau khi application đã ổn định.

---

# 6. Coding rules cho agent

## Ưu tiên

```text
simple code
explicit behavior
small methods
clear ownership
easy debugging
```

## Không được

```text
large architecture rewrite
rename projects
rename namespaces không cần thiết
new abstraction layer không có consumer thật
new NuGet không cần thiết
reflection framework mới
generic base classes chỉ để giảm vài dòng
```

## Extract code chỉ khi

```text
behavior thực sự giống nhau
+
responsibility giống nhau
+
sau extract code dễ hiểu hơn
```

Nếu chỉ giống cú pháp nhưng khác platform responsibility:

```text
giữ riêng
```

---

# 7. Logging rules

Không log:

```text
password
token
connection secret
sensitive parameter values
```

Module navigation parameter preview chỉ được ghi:

```text
type
property names
```

không ghi values.

Full exception có thể ghi vào diagnostic log nhưng không hiển thị UI.

---

# 8. Notification rules

Tất cả Module error Notification phải đi qua một nơi.

Không để:

```text
ModuleLoader notify
PageNavigator notify
MainLayout notify
ErrorBoundary notify
```

cùng lúc.

Ownership:

```text
Load/navigation error
    PageNavigator logs
    MainLayout safe-open calls ModuleErrorNotifier

Render/lifecycle error
    ModuleErrorBoundary logs
    ModuleErrorBoundary calls ModuleErrorNotifier
```

`ModuleErrorNotifier` chỉ quyết định:

```text
show?
message?
Radzen Notification
```

Không log.

---

# 9. Expected final structure

```text
Core/
│
├─ MAP.C.Contract/
│  ├─ Config/
│  ├─ Database/
│  ├─ Localization/
│  ├─ Logging/
│  ├─ Menus/
│  ├─ Models/
│  ├─ Modules/
│  └─ Navigation/
│
├─ MAP.C.Runtime/
│  ├─ Config/
│  ├─ Database/
│  ├─ Localization/
│  ├─ Logging/
│  │  └─ LogStoreLoggerProvider.cs
│  ├─ Menus/
│  │  └─ MenuConfigResolver.cs
│  └─ Navigation/
│
├─ MAP.C.UI/
│  ├─ Errors/
│  │  ├─ ModuleErrorBoundary.*
│  │  └─ ModuleErrorNotifier.cs
│  ├─ Headers/
│  ├─ Layout/
│  ├─ Localization/
│  ├─ Navigation/
│  ├─ Pages/
│  └─ wwwroot/
│
├─ MAP.C.Wpf/
│  ├─ Config/
│  ├─ Logging/
│  │  └─ FileLogStore.cs
│  ├─ Menus/
│  ├─ Modules/
│  ├─ DisplayHelper.cs
│  ├─ WpfHost.cs
│  ├─ WpfServices.cs
│  └─ MainWindow.*
│
└─ MAP.C.Wasm/
   ├─ Config/
   ├─ Logging/
   │  └─ IndexedDbLogStore.cs
   ├─ Menus/
   ├─ Modules/
   └─ Wasm bootstrap/runtime

MAP.H.Desktop/
    minimal bootstrap only

MAP.H.Web/
    minimal bootstrap
    wwwroot host requirements
    Modules/
    Web module build/package logic
```

---

# 10. Definition of Done

Cleanup chỉ được coi là hoàn tất khi:

- Không thay đổi 5-project Core architecture.
- Header API được giữ nguyên.
- Web Modules sync được giữ nguyên.
- C.Wpf tiếp tục sở hữu Desktop runtime.
- C.Wasm tiếp tục sở hữu Web runtime.
- H.Desktop mỏng.
- H.Web mỏng.
- Module không cần reference Wpf/Wasm/Hosts.
- LoggerProvider duplication được loại bỏ.
- Menu common logic không còn duplicate không cần thiết.
- WPF chỉ có một display enumeration/source.
- Desktop không lấy shared CSS từ Web.
- Duplicate static assets được giảm.
- Dead code được xóa sau usage search.
- Module DLL missing không làm app crash.
- Module type missing không làm app crash.
- Module localization failure không làm app crash.
- Module constructor/lifecycle/render failure không làm shell crash.
- Module event failure được test.
- Header/Menu vẫn hoạt động sau Module lỗi.
- User có thể mở Module khác sau Module lỗi.
- Raw exception không hiển thị trên UI.
- Module error hiển thị bằng Radzen Notification.
- Notification message có thể cấu hình.
- Notification có thể tắt bằng config.
- Tắt Notification không tắt logging.
- Module error log có ErrorId.
- Log chứa PageId/Assembly/Component/SessionId khi có thể.
- Logging failure không làm app crash.
- Debug build pass.
- Release build pass.
- Tests pass.
- Desktop publish pass.
- Web publish pass.
- `build.ps1` pass.
- `build-all.ps1` pass.
- Không chạy production deploy.
- Không commit/push/open PR nếu chưa được yêu cầu.

---

# 11. Agent final report

Sau khi hoàn thành, agent phải trả:

## Files changed

Danh sách file thêm/sửa/xóa.

## Work package result

```text
WP01 DONE / NOT DONE
WP02 DONE / NOT DONE
...
```

## Cleanup result

Nêu rõ:

```text
duplicate code đã xóa
dead code đã xóa
code intentionally kept
```

## Module safety result

Nêu kết quả từng fault test:

```text
assembly missing
type missing
localization failure
constructor
OnInitialized
OnParametersSet
OnAfterRender
event handler
Notification ON
Notification OFF
logging fallback
```

## Verification

Liệt kê từng command:

```text
command
PASS/FAIL
```

Nếu fail phải ghi root cause.

## Confirmations

Agent phải xác nhận:

```text
Header API unchanged
Web Modules sync unchanged
C.Wpf responsibility unchanged
C.Wasm responsibility unchanged
deploy scripts not intentionally refactored
no unnecessary new NuGet
no production deployment performed
no commit/push performed
```

## Remaining risks

Nếu có lỗi Module nào Blazor/.NET không thể cô lập an toàn thì ghi rõ.

Không che giấu limitation bằng catch-all exception handling.