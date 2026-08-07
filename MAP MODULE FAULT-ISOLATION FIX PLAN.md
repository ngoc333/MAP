# MAP MODULE FAULT-ISOLATION FIX PLAN

## 0. Scope

Fix các vấn đề còn lại sau review commit:

```text
5e2c1e1d43e12440e6d896a4f771a94f6bef09e6
```

Chỉ xử lý:

```text
- ErrorId correlation
- safe navigation từ Module
- ModuleErrorBoundary
- duplicate exception logging
- retry sau render failure
- module error log context
- localization của Notification
- MODULE_RULES.md / .gitignore
```

Không thực hiện:

```text
- asset cleanup
- thin H.Desktop
- thin H.Web
- Router cleanup
- deploy changes
- Web Modules sync changes
- Header API changes
```

Không commit/push/PR.

---

# 1. Mục tiêu cuối

Các lỗi Module managed thông thường phải có behavior:

```text
Module lỗi
    ↓
Core bắt lỗi
    ↓
ghi log đúng 1 lần ở ownership layer
    ↓
ErrorId trong log == ErrorId trên Notification
    ↓
Notification nếu config bật
    ↓
Shell vẫn sống
    ↓
Header/Menu vẫn hoạt động
    ↓
current page không bị mất nếu target navigation load lỗi
    ↓
user có thể retry/open module khác
```

Raw exception không được hiển thị cho user.

---

# WP01 — Correlate ErrorId giữa Notification và log

## Problem

Hiện `PageNavigator` log exception trước.

Sau đó `MainLayout.OpenPageSafeAsync()` mới tạo:

```csharp
Guid.NewGuid()
```

để đưa lên Notification.

Kết quả:

```text
Notification ErrorId != log
```

User báo ErrorId nhưng developer không search được log tương ứng.

---

## Fix

Tạo ErrorId tại nơi ownership của navigation failure:

```text
PageNavigator
```

Trong catch:

```csharp
catch (Exception ex)
{
    var errorId = ...
}
```

Log:

```text
Navigation failed.
ErrorId={ErrorId}
NavigationId={NavigationId}
PageId={PageId}
FromPageId={FromPageId}
...
```

Sau đó attach ErrorId vào exception trước khi rethrow.

Giải pháp đơn giản:

```csharp
ex.Data["MAP.ErrorId"] = errorId;
throw;
```

Không tạo Result framework.

Không tạo custom exception hierarchy nếu không cần.

---

## MainLayout

Khi catch:

```csharp
catch (Exception ex)
{
    var errorId = ModuleErrorId.GetOrCreate(ex);
    ErrorNotifier.Notify(errorId);
}
```

Có thể tạo một helper rất nhỏ:

```text
MAP.C.Runtime/Diagnostics/ModuleErrorId.cs
```

hoặc đặt helper ở vị trí chung hợp lý.

API tối đa:

```csharp
Create()
GetOrCreate(Exception ex)
Set(Exception ex, string errorId)
```

Không phức tạp hơn.

---

## Acceptance

Một navigation error:

```text
Notification:
ErrorId=A1234567
```

thì log phải chứa chính xác:

```text
ErrorId=A1234567
```

---

# WP02 — Safe navigation phải dùng được từ Module

## Problem

Safe navigation hiện chỉ nằm trong:

```text
MainLayout.OpenPageSafeAsync()
```

Nhưng Module vẫn gọi trực tiếp:

```csharp
Navigator.OpenAsync(...)
```

Ví dụ các button trong Module Home.

Nếu Module A mở Module B và B load fail:

```text
Navigator.OpenAsync throws
→ exception quay lại event handler Module A
→ Module A có thể bị ErrorBoundary bắt
→ current page A bị fault theo
```

Trong khi requirement là:

```text
B lỗi
→ A vẫn sống
```

---

## Fix

Đưa safe navigation API xuống `BasePage`.

Ví dụ:

```csharp
protected async Task OpenPageAsync(
    string pageId,
    object? parameters = null)
{
    try
    {
        await Navigator.OpenAsync(pageId, parameters);
    }
    catch (Exception ex)
    {
        ErrorNotifier.Notify(
            ModuleErrorId.GetOrCreate(ex));
    }
}
```

`BasePage` inject:

```text
IPageNavigator
ModuleErrorNotifier
```

nếu chưa có.

Logging vẫn do `PageNavigator`.

`BasePage` không log exception lại.

---

## Update Modules

Repository-wide search:

```text
Navigator.OpenAsync(
```

trong `Modules/`.

Đổi navigation do Module UI phát sinh thành:

```text
OpenPageAsync(...)
```

Không expose raw navigation path cho normal module page code nếu không cần.

---

## MainLayout

Có thể giữ:

```text
OpenPageSafeAsync()
```

cho shell navigation.

Nếu code logic giống hoàn toàn với BasePage, có thể extract helper nhỏ.

Nhưng không tạo:

```text
INavigationCoordinator
INavigationService2
NavigationManagerFactory
```

chỉ để tránh vài dòng.

---

## Acceptance

Test:

```text
Module A đang hiển thị
    ↓
button A mở Module B
    ↓
B DLL missing
```

Expected:

```text
A vẫn hiển thị
A không vào ErrorBoundary
stack unchanged
Notification xuất hiện
log có exception
```

---

# WP03 — Implement ModuleErrorBoundary đúng hook

## Problem

Hiện:

```razor
<ErrorBoundary>
    ...
    <ErrorContent>
        @{ HandleError(exception); }
    </ErrorContent>
</ErrorBoundary>
```

`HandleError()` thực hiện side-effect trong render.

Ngoài ra Blazor ErrorBoundary mặc định cũng có logging behavior.

Có nguy cơ:

```text
duplicate LogError
```

và side-effect trong render không sạch.

---

## Fix

Tạo custom boundary thực sự.

Preferred:

```text
ModuleErrorBoundary.cs
```

inherit:

```csharp
ErrorBoundary
```

Override:

```csharp
protected override Task OnErrorAsync(
    Exception exception)
```

Tại đây:

```text
1. Get/Create ErrorId
2. lấy module context
3. LogError full exception
4. ErrorNotifier.Notify(errorId)
5. return Task.CompletedTask
```

Không rethrow.

---

## Razor/UI

Boundary chỉ render:

```text
ChildContent khi bình thường
minimal/empty content khi lỗi
```

Không hiện:

```text
exception.Message
stack trace
technical details
error panel
Retry button
Back button
```

Thông báo chính là Radzen Notification.

---

# WP04 — Add module context vào render error log

Render/lifecycle log phải có tối đa context có thể lấy được:

```text
ErrorId
PageId
Assembly
Component
SessionId
OperationId
```

Inject:

```text
IPageNavigator
```

vào custom boundary.

Lấy:

```csharp
var active = Navigator.Current;
```

Log:

```text
Module render/lifecycle failed.
ErrorId={ErrorId}
PageId={PageId}
Assembly={Assembly}
Component={Component}
SessionId={SessionId}
OperationId={OperationId}
```

Full exception truyền vào:

```csharp
Logger.LogError(exception, ...)
```

Không cần duplicate exception properties thủ công.

---

# WP05 — Remove duplicate ModuleLoader full exception logging

## Problem

Hiện:

```text
ModuleLoader
    LogError(ex)
    throw

PageNavigator
    LogError(ex)
```

Một lỗi load Module ghi full stack hai lần.

---

## Ownership mới

### ModuleLoader

Được phép log:

```text
Information
Debug
Warning nếu là non-fatal internal condition
```

Ví dụ:

```text
loading assembly
cache hit
localization starting
duration
```

Nhưng failure cuối cùng của navigation không log full exception tại đây nếu sẽ rethrow cho PageNavigator.

Đổi:

```csharp
catch (Exception ex)
{
    Logger.LogError(ex, ...);
    throw;
}
```

thành:

```csharp
catch
{
    throw;
}
```

hoặc bỏ catch hoàn toàn nếu chỉ để rethrow.

Giữ `finally` cho:

```text
OnLoadingChanged(false)
```

---

## PageNavigator

Là owner của:

```text
module load/navigation exception
```

và log full exception đúng một lần.

---

## Exception

Giữ root cause.

Không:

```csharp
throw new Exception(..., ex);
```

nếu không thật sự cần.

Preferred:

```csharp
throw;
```

---

# WP06 — Retry current Module sau render failure

## Problem

Sau render error, ErrorBoundary giữ faulted state.

Nếu user click lại cùng menu:

```csharp
if (Current.PageId == pageId &&
    parameters is null)
{
    return;
}
```

thì navigator không tạo ActivePage mới.

Kết quả:

```text
click lại menu
→ không retry
```

---

## Requirement

Sau Module render/lifecycle lỗi:

```text
user click lại cùng menu
→ module được recreate
→ có thể retry
```

Không cần Retry button riêng.

---

## Simple implementation

Custom `ModuleErrorBoundary` khi fault có thể báo trạng thái lên page container/shell.

Preferred simple approach:

`PageContainer` giữ một generation/error state.

Ví dụ:

```text
ModuleErrorBoundary.OnError
    ↓
PageContainer biết current render đã fault
```

Sau đó khi click lại cùng page, phải có cách tạo `ActivePage`/key mới.

Không over-engineer.

---

## Một option đơn giản hơn

Thêm vào `IPageNavigator`:

```csharp
Task ReloadAsync();
```

chỉ nếu implementation thật sự đơn giản và được dùng.

Nhưng ưu tiên trước:

```text
click cùng menu sau fault
→ force reopen
```

thay vì thêm public API nếu không cần.

Có thể để `MainLayout/PageContainer` lưu:

```text
faultedPageId
```

và khi menu được chọn:

```text
if selected page == faultedPageId
    force navigation/recreate
```

Chọn implementation ít code nhất.

---

## Important

Không phá behavior hiện tại:

```text
click cùng menu bình thường
→ vẫn skip
```

Chỉ retry khi page đã fault.

---

# WP07 — Notification localization cleanup

Hiện Notification dùng:

```text
moduleError.title
moduleError.defaultMessage
```

nhưng Core localization chưa đủ.

---

## Add Core localization

`Core/MAP.C.Runtime/Localization/vi.json`

thêm:

```json
"moduleError": {
  "title": "Lỗi chức năng",
  "defaultMessage": "Chức năng tạm thời không khả dụng.",
  "errorId": "Mã lỗi"
}
```

`en.json`:

```json
"moduleError": {
  "title": "Function error",
  "defaultMessage": "This function is temporarily unavailable.",
  "errorId": "Error ID"
}
```

---

## ModuleErrorNotifier

Không hard-code:

```text
Mã lỗi
```

Dùng localization.

---

## Avoid duplicated title

Hiện có thể đang tạo:

```text
Summary = "Lỗi chức năng"

Detail =
"Lỗi chức năng

Chức năng...
Mã lỗi..."
```

Không lặp Summary trong Detail.

Expected:

```text
Summary:
Lỗi chức năng

Detail:
Chức năng tạm thời không khả dụng.
Mã lỗi: ABCD1234
```

---

# WP08 — Notification robustness

`ModuleErrorNotifier.Notify()` không được throw ngược làm error handling tự lỗi.

Wrap tối thiểu:

```csharp
try
{
    NotificationService.Notify(...);
}
catch (Exception ex)
{
    Debug.WriteLine(...);
}
```

Không cần log qua ILogger nếu điều đó có nguy cơ recursive logging/error handling.

Mục tiêu:

```text
Notification lỗi
≠
application lỗi thêm lần nữa
```

---

# WP09 — Logging fallback cleanup

`LogStoreLoggerProvider` hiện fire-and-forget:

```csharp
_ = WriteAsync(...)
```

Giữ behavior non-blocking nếu cần.

Nhưng phải chắc:

```text
store.WriteAsync throw
→ Debug fallback
→ không unobserved exception
```

Current wrapper async catch có thể giữ nếu đúng.

Review thêm:

```text
FileLogStore.WriteAsync
IndexedDbLogStore.WriteAsync
```

để tránh double-swallow.

Preferred ownership:

```text
ILogStore
    thực hiện persistence
    throw khi persistence failure

LogStoreLoggerProvider
    catch
    Debug/Console fallback
```

Không để cả Store và Provider cùng swallow cùng lỗi.

---

# WP10 — MODULE_RULES.md

Tạo và commit:

```text
MODULE_RULES.md
```

Không ignore file này.

Nội dung ngắn.

---

## Required rules

### Module references

Allowed:

```text
MAP.C.Contract
MAP.C.UI
Radzen.Blazor
```

Không direct reference:

```text
MAP.C.Wpf
MAP.C.Wasm
MAP.H.Desktop
MAP.H.Web
```

---

### Navigation

Module page dùng:

```csharp
OpenPageAsync(...)
```

Không dùng raw:

```csharp
Navigator.OpenAsync(...)
```

cho normal UI navigation.

---

### Async

Không dùng:

```csharp
async void
```

trừ event API bắt buộc.

Không:

```csharp
_ = SomeAsync();
```

nếu task có thể throw mà không được observe.

---

### Process control

Không gọi trực tiếp:

```text
Environment.Exit
Environment.FailFast
Process.Kill
Application.Current.Shutdown
```

---

# WP11 — Fix .gitignore

Hiện `.gitignore` quá rộng:

```text
MAP*.md
MODULE_RULES.md
```

Không ignore `MODULE_RULES.md`.

Thay wildcard rộng bằng tên review artifacts cụ thể nếu cần.

Ví dụ:

```text
MAP_CORE_STABILITY_PLAN.md
MAP_REVIEW_FIX_REQUEST*.md
MAP_CORE_STRUCTURE_CLEANUP.md
```

Không dùng:

```text
MAP*.md
```

vì có thể vô tình ignore tài liệu chính thức của project sau này.

---

# WP12 — Tests bắt buộc

## TEST01 — Navigation ErrorId correlation

Force module load fail.

Assert:

```text
log ErrorId == Notification ErrorId
```

Nếu test Notification trực tiếp khó:

- unit test helper ErrorId.
- integration/manual verify actual toast + log.

---

# TEST02 — Module A opens broken Module B

Module A đang chạy.

A gọi:

```text
OpenPageAsync("broken-page")
```

B load fail.

Expected:

```text
A vẫn render
Navigator.Current == A
Notification ON → toast
Notification OFF → no toast
log full exception
same ErrorId
```

---

# TEST03 — Missing assembly from menu

Expected:

```text
current page unchanged
stack unchanged
one full LogError
Notification optional
```

Kiểm tra log không có duplicate full exception từ:

```text
ModuleLoader
PageNavigator
```

---

# TEST04 — Missing component type

Giống TEST03.

---

# TEST05 — Localization load failure

Expected:

```text
current page unchanged
assembly không commit như initialized success
retry hoạt động
one full exception log
```

---

# TEST06 — Constructor failure

Temporary test component throw trong constructor.

Expected:

```text
ModuleErrorBoundary handles
shell alive
Header alive
Menu alive
Notification
log:
    ErrorId
    PageId
    Assembly
    Component
    SessionId
    full exception
```

---

# TEST07 — OnInitialized failure

Expected giống TEST06.

---

# TEST08 — OnParametersSet failure

Expected giống TEST06.

---

# TEST09 — Render failure

Expected giống TEST06.

---

# TEST10 — OnAfterRender failure

Expected:

```text
shell alive
notification
log
can navigate away
```

---

# TEST11 — Event handler failure

Button trong Module throw.

Expected:

```text
boundary contains module fault
shell alive
notification
log
```

Xác nhận behavior thực tế trên cả:

```text
WPF BlazorWebView
WebAssembly
```

---

# TEST12 — Retry faulted page

Module X throw render exception.

Sau đó sửa/remove test fault hoặc sử dụng fault-once component.

Click lại cùng menu X.

Expected:

```text
new component instance
boundary recovered/recreated
page hoạt động
```

---

# TEST13 — Notification OFF

```json
{
  "showModuleErrorNotification": false
}
```

Expected:

```text
no toast
log vẫn có full error
shell alive
```

---

# TEST14 — Notification custom message

```json
{
  "showModuleErrorNotification": true,
  "moduleErrorMessage": "Không thể mở chức năng."
}
```

Expected:

```text
Summary:
Lỗi chức năng

Detail:
Không thể mở chức năng.
Mã lỗi: XXXXXXXX
```

Không raw exception.

---

# 2. Unit tests cần bổ sung

## PageNavigatorTests

Giữ các test hiện tại.

Thêm test:

```text
failure leaves current stack unchanged
ErrorId attached to thrown exception
same ErrorId is logged/correlatable
```

Nếu kiểm tra structured ILogger phức tạp, dùng test logger nhỏ.

Không thêm logging test framework NuGet chỉ cho việc này.

---

## ErrorId helper tests

Nếu tạo helper:

```text
Create_Returns8CharacterId
GetOrCreate_ReturnsExistingId
GetOrCreate_CreatesWhenMissing
```

---

# 3. Code cleanup trong scope này

Sau khi fix xong:

Search:

```text
Navigator.OpenAsync(
```

trong Modules.

Normal Module UI navigation không nên còn gọi raw Navigator.

Search:

```text
LogError(
```

trong WPF/Wasm ModuleLoader.

Không nên còn duplicate final failure logging nếu PageNavigator sở hữu exception.

Search:

```text
exception.Message
```

trong Module error UI.

Không hiển thị.

Search:

```text
ErrorContent
```

đảm bảo không còn side-effect notification/logging trong render fragment.

---

# 4. Build verification

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

Sau đó:

```powershell
dotnet publish MAP.H.Desktop/MAP.H.Desktop.csproj -c Release
```

```powershell
dotnet publish MAP.H.Web/MAP.H.Web.csproj -c Release
```

Nếu build scripts có thể chạy:

```powershell
.\build.ps1
```

```powershell
.\build-all.ps1
```

Không chạy deploy.

---

# 5. Manual verification

Agent phải kiểm tra hoặc ghi rõ chưa thể kiểm tra:

```text
Desktop WPF
WebAssembly
```

Các case tối thiểu:

```text
missing DLL
wrong component
module A → broken module B
render exception
event-handler exception
Notification ON
Notification OFF
retry same faulted module
```

Nếu môi trường agent không chạy được UI:

```text
không được ghi PASS giả
```

Phải ghi:

```text
NOT RUN - requires manual runtime verification
```

và cung cấp exact manual test steps.

---

# 6. Không được thay đổi

Agent phải xác nhận không thay đổi:

```text
HeaderKind
HeaderStart
HeaderCenter
HeaderEnd

Web Modules sync
MAP.H.Web/Modules design

C.Wpf responsibility
C.Wasm responsibility

config/log location

deploy scripts behavior
```

Không thêm new NuGet nếu không cần.

Không rename project/namespace/public API ngoài những API được nêu trong plan.

---

# 7. Expected logging ownership cuối cùng

## Navigation/load failure

```text
ModuleLoader
    loading/cache/timing only

PageNavigator
    ErrorId
    full exception
    navigation/module context

MainLayout/BasePage
    notification only
```

---

## Render/lifecycle failure

```text
ModuleErrorBoundary
    ErrorId
    full exception
    module context
    notification
```

---

## Notification

```text
ModuleErrorNotifier
    config
    localization
    Radzen Notification

NO full exception logging
```

---

# 8. Definition of Done

Chỉ DONE khi:

- Notification ErrorId trùng log ErrorId.
- Module A mở Module B lỗi thì A vẫn sống.
- Module navigation từ Module dùng safe API.
- `ModuleErrorBoundary` dùng proper error hook.
- Không side-effect trong `ErrorContent`.
- Module load failure không full-log hai lần.
- Render error log có PageId/Assembly/Component.
- Raw exception không xuất hiện trên UI.
- Notification được localization đầy đủ.
- Summary không bị duplicate trong Detail.
- Notification OFF vẫn ghi log.
- User có thể click lại faulted module để retry.
- `MODULE_RULES.md` được commit vào repository.
- `.gitignore` không ignore `MODULE_RULES.md`.
- Unit tests pass.
- Debug/Release build pass.
- Desktop/Web publish pass nếu môi trường hỗ trợ.
- Không thay đổi deploy.
- Không thay đổi Header API.
- Không thay đổi Web Modules sync.
- Không commit/push/PR.

---

# 9. Agent final report

Agent phải trả report theo format:

## Changed files

```text
Added:
Modified:
Deleted:
```

## Findings fixed

```text
F01 ErrorId correlation          DONE / NOT DONE
F02 Module safe navigation       DONE / NOT DONE
F03 ErrorBoundary implementation DONE / NOT DONE
F04 Duplicate logging            DONE / NOT DONE
F05 Faulted module retry         DONE / NOT DONE
F06 Module log context           DONE / NOT DONE
F07 Notification localization    DONE / NOT DONE
F08 MODULE_RULES/.gitignore      DONE / NOT DONE
```

## Tests

```text
TEST01 ... PASS / FAIL / NOT RUN
...
```

## Commands

Liệt kê:

```text
command
result
```

## Manual verification

```text
Desktop:
Web:
```

## Confirmations

```text
Header API unchanged
Web Modules sync unchanged
C.Wpf/C.Wasm responsibilities unchanged
deploy unchanged
no unnecessary NuGet
no commit/push/PR
```

## Remaining limitations

Nêu rõ bất kỳ lỗi Blazor/.NET nào không thể isolate an toàn.

Không che limitation bằng catch-all exception handling.