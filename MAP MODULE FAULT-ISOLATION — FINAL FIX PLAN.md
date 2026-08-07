# MAP MODULE FAULT-ISOLATION — FINAL FIX PLAN

## 0. Baseline

Current commit:

```text id="zq6sn8"
da4110e425b80208891dbffcf930d50ac5721265
```

Mục tiêu:

```text id="v37wx0"
Fix regression:
same page + new parameters must recreate page instance
```

Cleanup thêm:

```text id="nfp262"
- remove Result.Fail example from MODULE_RULES.md
- remove unused IModuleLoader injection from MainLayout
```

Không làm thêm bất kỳ cleanup nào khác.

Không commit/push/open PR.

---

# F01 — Fix PageContainer key regression

## Problem

Hiện MainLayout dùng:

```razor id="aox60a"
<PageContainer
    Type="active.ComponentType"
    @key="@(active.PageId + ":" + _pageGeneration)"
    OnFaulted="OnPageFaulted" />
```

Điều này phá behavior cũ.

Scenario:

```text id="52cwx7"
Current:
product-add
parameters = SP001

↓ OpenAsync same page

product-add
parameters = SP999
```

`PageNavigator` tạo `ActivePage` mới nhưng:

```text id="djx8on"
PageId vẫn product-add
_pageGeneration không đổi
```

nên key không đổi.

Kết quả:

```text id="rhksmz"
PageContainer / DynamicComponent có thể giữ instance cũ
OnInitialized không chạy lại
PageParameters mới không được dùng để initialize page
```

Đây là regression.

---

# F02 — Preserve navigation identity AND retry generation

Requirement:

```text id="2d65xl"
ActivePage mới
    → component tree mới

Retry faulted page
    → component tree mới

Navigation stack
    → không đổi chỉ vì retry
```

Không được dùng chỉ:

```text id="9zzbku"
PageId
```

làm page identity.

---

## Preferred implementation

Giữ `ActivePage` làm navigation key như trước.

Dùng generation ở một wrapper bên ngoài.

Concept:

```razor id="jlyl8z"
@if (Navigator.Current is { } active)
{
    <div @key="_pageGeneration">
        <PageContainer
            Type="active.ComponentType"
            @key="active"
            OnFaulted="OnPageFaulted" />
    </div>
}
```

Behavior:

### Normal different-page navigation

```text id="hwvos5"
ActivePage A
→ ActivePage B

@key active changes
→ PageContainer recreated
```

### Same-page navigation with new parameters

```text id="fgkfbz"
ActivePage product-add/SP001
→ new ActivePage product-add/SP999

@key active changes
→ PageContainer recreated
→ module component recreated
→ OnInitialized sees SP999
```

### Retry render-faulted page

```text id="h6rdpl"
same ActivePage
_pageGeneration++

wrapper key changes
→ entire page subtree recreated
→ new ModuleErrorBoundary
→ new DynamicComponent
```

Navigation stack remains unchanged.

---

## Alternative

Nếu không muốn render `<div>`, có thể tạo wrapper component rất nhỏ.

Nhưng không tạo:

```text id="viw3al"
PageHostManager
NavigationRenderCoordinator
ModuleRenderService
```

hoặc abstraction mới.

Ưu tiên solution ít code nhất.

---

# F03 — Retry behavior must remain

Current logic:

```csharp id="fvsofe"
if (FaultedPageId == menuItem.Id)
{
    FaultedPageId = null;
    _pageGeneration++;
    StateHasChanged();
    return;
}
```

Giữ behavior này hoặc equivalent.

Không gọi:

```csharp id="ljia1y"
Navigator.OpenAsync(...)
```

chỉ để retry render fault.

Reason:

```text id="7ll0x7"
retry render fault
≠ navigation
```

Không thêm navigation history entry.

---

# F04 — Preserve normal same-page navigation behavior

Không thay đổi `PageNavigator`.

Giữ:

```csharp id="1m27a6"
OpenAsync(pageId, parameters: null)
```

khi cùng current page:

```text id="xc034y"
skip
```

Giữ:

```csharp id="dcy4bf"
OpenAsync(pageId, parameters != null)
```

khi cùng current page:

```text id="u62bht"
prepare new page
replace top ActivePage
preserve FromPageId
```

Không thêm lại:

```text id="gcoo99"
forceReopen
bool overload
ReloadAsync
```

vào `IPageNavigator`.

Public Contract phải giữ:

```csharp id="sx347h"
Task OpenAsync(
    string pageId,
    object? parameters = null);

Task BackAsync();
```

---

# F05 — Regression test: same page + new parameters

Đây là test quan trọng nhất.

Có thể test ở UI/manual level nếu hiện test project chưa hỗ trợ Blazor component rendering.

Scenario:

```text id="gjxiut"
1. Open product-add với:
   Id = SP001
   Name = Product 1

2. product-add render

3. Open product-add lần nữa với:
   Id = SP999
   Name = Product 999
```

Expected:

```text id="cz4wlw"
new ActivePage instance
new PageContainer/component instance
ProductAdd.OnInitialized executes for new instance
form shows SP999
not SP001
```

Navigation stack depth không tăng.

---

# F06 — Regression test: retry faulted page

Use temporary fault-once component.

Behavior:

```text id="hva9w8"
instance #1
    → throws during render/init

click same menu

instance #2
    → succeeds
```

Expected:

```text id="c5gkll"
ModuleErrorBoundary catches first failure
Notification shown
Header/Menu remain alive

click same page
→ page subtree recreated
→ component instance #2
→ page succeeds
```

Assert/conclude:

```text id="rp0rc9"
navigation stack unchanged
ActivePage unchanged during retry
```

---

# F07 — Regression test: fault A → broken B → retry A

Scenario:

```text id="t2xeg4"
A renders and faults
FaultedPageId = A

user attempts B
B module load fails

Navigator.Current remains A
```

Expected:

```text id="obbejq"
FaultedPageId remains A
B failure does not replace fault state

click A
→ generation increments
→ A recreated
```

---

# F08 — Verify normal navigation after retry

After successful retry:

```text id="hv932p"
FaultedPageId == null
```

Then navigate:

```text id="a2j4x9"
A → C
```

Expected normal behavior:

```text id="7zhpg0"
Navigator changes ActivePage
new page renders normally
generation value is irrelevant
```

No special retry behavior should leak into later pages.

---

# F09 — Remove Result.Fail from MODULE_RULES.md

Current document has example like:

```csharp id="q4l1ok"
return Result.Fail("...");
```

MAP intentionally does not provide a Result framework.

Remove this example.

Use simple examples such as:

```csharp id="j0o7qp"
if (string.IsNullOrWhiteSpace(model.Code))
{
    NotificationService.Notify(...);
    return;
}
```

or just document:

```text id="tffqbq"
validate
notify user
stop current action
```

Do not introduce:

```text id="8kl2h4"
Result<T>
OperationResult
Either
OneOf
```

or new NuGet.

---

# F10 — Remove unused MainLayout injection

Search MainLayout.

If:

```razor id="n0mrg8"
@inject IModuleLoader ModuleLoader
```

has zero usage, remove it.

Do not modify `IModuleLoader`.

Do not change PageContainer loading behavior.

---

# F11 — Search checks

After changes:

Search:

```text id="de681h"
forceReopen
```

Expected:

```text id="0jzah4"
0 production results
```

Search:

```text id="20gq8l"
Result.Fail
```

Expected:

```text id="yspb00"
0 results in MODULE_RULES.md
```

Search:

```text id="kp3i24"
@inject IModuleLoader ModuleLoader
```

MainLayout should no longer contain it.

PageContainer may still use `IModuleLoader`.

---

# F12 — Build/test verification

Run:

```powershell id="w8glz9"
dotnet restore MAP.slnx
```

```powershell id="6a62jm"
dotnet build MAP.slnx -c Debug --no-restore
```

```powershell id="9t1mg0"
dotnet build MAP.slnx -c Release --no-restore
```

```powershell id="dqzi8i"
dotnet test Tests/MAP.C.Runtime.Tests/MAP.C.Runtime.Tests.csproj -c Release
```

Then:

```powershell id="07dmwd"
dotnet publish MAP.H.Desktop/MAP.H.Desktop.csproj -c Release
```

```powershell id="8x4qbr"
dotnet publish MAP.H.Web/MAP.H.Web.csproj -c Release
```

If environment supports:

```powershell id="tz3ntb"
.\build.ps1
.\build-all.ps1
```

Do not deploy.

---

# F13 — Manual verification

Agent must run if runtime environment is available.

Otherwise mark:

```text id="ywoig0"
NOT RUN
```

Do not claim PASS.

---

## TEST01 — same page with new parameters

```text id="ylxe0m"
product-add SP001
→ product-add SP999
```

Expected:

```text id="uc158f"
SP999 shown
component recreated
stack depth unchanged
```

---

## TEST02 — render fault retry

```text id="8195ts"
faulted A
→ click A
```

Expected:

```text id="j091ct"
new component instance
A retries
stack unchanged
```

---

## TEST03 — fault A then broken B

```text id="10c0gb"
A faulted
→ B DLL missing
→ retry A
```

Expected:

```text id="jatqpr"
current stays A
A remains retryable
B failure notification shown
```

---

## TEST04 — normal page switch

```text id="3ro2tx"
A → B → C
```

Expected normal navigation behavior unchanged.

---

## TEST05 — same page without parameters

```text id="nfgc6f"
A
→ OpenAsync(A)
```

Expected:

```text id="ar8qq2"
skip
same ActivePage
same component instance
```

unless page is explicitly in render-fault retry path.

---

# Do not change

Keep unchanged:

```text id="8hkpmz"
IPageNavigator public API
ModuleErrorId behavior
ModuleErrorBoundary
ModuleErrorNotifier behavior
BasePage.OpenPageAsync
PageNavigator navigation semantics

HeaderKind
HeaderStart
HeaderCenter
HeaderEnd

IPlatformCapabilities
IAppConfigService.GetSystemInfo

Web Modules sync
C.Wpf ownership
C.Wasm ownership

config/log location
deploy scripts
```

No new NuGet.

No new architecture framework.

---

# Definition of Done

```text id="zv1det"
[ ] ActivePage remains navigation identity
[ ] retry generation can recreate same ActivePage
[ ] same page + new parameters recreates component
[ ] ProductAdd receives new parameters correctly
[ ] same page + null parameters still skips
[ ] retry does not modify navigation stack
[ ] fault A → broken B preserves A fault state
[ ] no forceReopen API
[ ] no new public navigation API
[ ] Result.Fail example removed
[ ] unused MainLayout IModuleLoader injection removed
[ ] Debug build passes
[ ] Release build passes
[ ] Runtime tests pass
[ ] Desktop publish passes if supported
[ ] Web publish passes if supported
[ ] manual regression tests PASS or explicitly NOT RUN
[ ] no deploy
[ ] no commit/push/PR
```

---

# Agent final report

## Changed files

```text id="o0puvw"
Modified:
Deleted:
Added:
```

## Findings

```text id="75rv0q"
F01 Page key regression        DONE / NOT DONE
F02 Retry generation           DONE / NOT DONE
F03 Same-page parameter test   DONE / NOT DONE
F04 MODULE_RULES cleanup       DONE / NOT DONE
F05 MainLayout dead injection  DONE / NOT DONE
```

## Verification

```text id="y78xdb"
Same-page new parameters       PASS / FAIL / NOT RUN
Faulted-page retry             PASS / FAIL / NOT RUN
Fault A → broken B → retry A   PASS / FAIL / NOT RUN
Normal navigation              PASS / FAIL / NOT RUN
Same-page no params skip       PASS / FAIL / NOT RUN
```

## Commands

For every command:

```text id="0crg2j"
COMMAND
PASS / FAIL / NOT RUN
```

## Confirmations

```text id="c70yxp"
IPageNavigator unchanged
Header API unchanged
Web Modules sync unchanged
C.Wpf/C.Wasm responsibilities unchanged
deploy unchanged
no unnecessary NuGet
no commit/push/PR
```

## Remaining limitations

Only list genuine remaining limitations.

Do not add additional abstractions or cleanup beyond this plan.