# MAP — Review Fix Request

## Commit cần sửa

- Repository: `ngoc333/MAP`
- Commit đã review: `743e186e9010aee154b36c197aed79c4c0f0e010`
- Commit cha: `b92c75f7a92c660f029baa404d2d57cb3be03777`

## Quyết định phạm vi

1. **WP02 không thực hiện.**
   - Không sửa `Run-App`.
   - Không thay đổi cơ chế đồng bộ/copy file của launcher.

2. **Chấp nhận thiết kế WP06 hiện tại:**
   - `app-config.json` được phép đặt cạnh executable.
   - Thư mục `log` được phép đặt cạnh executable.
   - Không chuyển config/log sang `LocalAppData`.
   - Không sửa đường dẫn này chỉ vì lý do quyền ghi thư mục.

3. Không sửa source trong `Modules/`, trừ khi cần điều chỉnh tối thiểu để project build sau khi contract thay đổi.

4. Không commit, không push. Chỉ sửa source, chạy build/test và báo cáo kết quả.

---

# P1 — Bắt buộc sửa

## ISSUE-01 — Mở lại cùng page có thể làm mất navigation state

### Vấn đề

Trong `PageNavigator.OpenAsync()`, khi page hiện tại trùng `pageId` và có parameters mới, code gọi `_stack.Pop()` trước khi:

- Tìm menu.
- Chuyển đổi parameters.
- Load component.
- Xác nhận toàn bộ thao tác thành công.

Nếu module loading hoặc menu lookup lỗi, page hiện tại đã bị xóa khỏi stack nhưng UI chưa nhận được `Changed`. Navigation state và UI có thể không đồng nhất.

Ngoài ra, `fromPageId` được lấy trước khi pop nên khi thay thế cùng page, page mới có thể nhận `FromPageId` bằng chính page hiện tại. 

### Yêu cầu sửa

- Không thay đổi stack trước khi menu và component được load thành công.
- Tạo `ActivePage` mới trước.
- Chỉ sau khi toàn bộ bước chuẩn bị thành công mới:
  - Pop page hiện tại nếu đây là thao tác replace.
  - Push page mới.
  - Gọi `Changed`.
- Nếu load lỗi:
  - Stack phải giữ nguyên.
  - `Current` phải giữ nguyên.
  - Parameters cũ phải giữ nguyên.
- Khi replace cùng page:
  - Giữ lại `FromPageId` của page cũ.
  - Không đặt `FromPageId` bằng chính `pageId` hiện tại.

### Render lại cùng page

`MainLayout` hiện dùng:

```razor
@key="active.PageId"
```

Khi mở lại cùng page với parameters mới, key không đổi nên page component có thể được tái sử dụng thay vì tạo instance mới. `BasePage` đọc parameters từ `Navigator.Current`, nhưng không đăng ký `Navigator.Changed`; những page đọc parameters trong `OnInitialized` sẽ không được khởi tạo lại.  

Thay key bằng navigation instance, ví dụ:

```razor
<PageContainer Type="active.ComponentType" @key="active" />
```

Kết quả mong muốn:

- Đổi ngôn ngữ không tạo lại page vì `ActivePage` không đổi.
- Mở lại cùng page với parameters mới sẽ tạo page instance mới vì `ActivePage` đã đổi.
- Mở page khác vẫn hoạt động bình thường.

### Tests bắt buộc

Bổ sung `PageNavigatorTests`:

1. `OpenAsync_FirstPage_PushesPage`
2. `OpenAsync_SamePageWithoutParameters_DoesNothing`
3. `OpenAsync_SamePageWithParameters_ReplacesTopWithoutIncreasingDepth`
4. `OpenAsync_SamePageWithParameters_PreservesPreviousFromPageId`
5. `OpenAsync_SamePageWhenLoaderFails_KeepsExistingPage`
6. `OpenAsync_MenuNotFound_KeepsExistingStack`
7. `BackAsync_WithMultiplePages_ReturnsToPreviousPage`
8. `ChangedSubscriberThrows_DoesNotFailNavigation`

---

## ISSUE-02 — Parameter preview có thể làm lộ dữ liệu nhạy cảm

### Vấn đề

`CreateParameterPreview()` serialize toàn bộ parameters rồi dùng regex để che dữ liệu.

Regex hiện tại:

- Chỉ bắt property bắt đầu bằng `password`, `token`, `secret` hoặc `key`.
- Không bắt đúng các tên như:
  - `AccessToken`
  - `ApiKey`
  - `ClientSecret`
  - `UserPassword`
- Chỉ xử lý giá trị dạng string.
- Phần replacement có thể tạo chuỗi JSON không hợp lệ.
- Parameters được ghi ở mức `Information`, nên dữ liệu nhạy cảm có thể tồn tại trong log. 

### Yêu cầu sửa

Ưu tiên cách đơn giản và an toàn:

- Không log giá trị parameters.
- Chỉ log:
  - Tên type.
  - Danh sách tên property.
  - Hoặc số lượng property.

Ví dụ:

```text
Type=LoginRequest Properties=[UserName,Password,RememberMe]
```

Không dùng regex để sửa JSON đã serialize.

Nếu vẫn cần JSON preview:

- Parse thành `JsonNode` hoặc `JsonElement`.
- Duyệt đệ quy object và array.
- Che mọi property có tên chứa, không phân biệt hoa thường:
  - `password`
  - `token`
  - `secret`
  - `key`
  - `credential`
  - `authorization`
- Che mọi loại giá trị, không chỉ string.

### Tests bắt buộc

Kiểm tra ít nhất:

- `Password`
- `userPassword`
- `accessToken`
- `apiKey`
- `clientSecret`
- Nested object.
- Array chứa object.
- Circular reference hoặc object không serialize được không làm navigation thất bại.

---

## ISSUE-03 — Deploy vẫn có thể báo thành công khi thiếu output

### Vấn đề

Deploy script đã bỏ password literal khỏi source. Tuy nhiên script vẫn tiếp tục khi thiếu các output quan trọng.

Ví dụ trong `deploy-all.ps1`:

- Không tìm thấy `Run-App.exe` chỉ warning.
- Không có `publish\desktop` chỉ warning.
- Không có `publish\web` chỉ warning.
- Cuối script vẫn có thể in `DEPLOY ALL COMPLETE`. 

### Yêu cầu sửa

Các thành phần bắt buộc bị thiếu phải kết thúc script với exit code khác 0.

Đối với `deploy.ps1`, kiểm tra bắt buộc:

- Build script tồn tại và chạy thành công.
- `Run-App.exe` tồn tại.
- `publish\core` tồn tại và có DLL.
- `publish\modules` tồn tại và có DLL.
- Desktop publish tồn tại.
- Web publish tồn tại.
- `msdeploy.exe` tồn tại.

Đối với `deploy-all.ps1`, kiểm tra bắt buộc:

- `Run-App.exe` tồn tại.
- `publish\desktop` tồn tại và không rỗng.
- `publish\web` tồn tại và không rỗng.
- Robocopy exit code nhỏ hơn 8.
- MsDeploy exit code bằng 0.

Chỉ in `DEPLOY COMPLETE` khi tất cả bước hoàn thành.

### Deploy settings

Chuyển các giá trị sau sang environment variable hoặc `.env`:

```text
MAP_DESKTOP_DEPLOY_PATH
MAP_WEB_DEPLOY_URL
MAP_WEB_DEPLOY_DEST
MAP_WEB_DEPLOY_USER
MAP_WEB_DEPLOY_PASSWORD
```

`.env.example` chỉ chứa placeholder, không chứa thông tin thật.

Có thể tạo helper chung:

```powershell
Get-RequiredDeploySetting
```

Thứ tự đọc:

1. Process environment variable.
2. `.env`.
3. Nếu thiếu thì báo rõ tên setting và `exit 1`.

Không in password ra console hoặc command summary.

### Debug symbols

Không tự động xóa toàn bộ `.pdb` khỏi publish output. Dự án ưu tiên khả năng debug; PDB phải được giữ lại trừ khi có cấu hình deploy riêng yêu cầu loại bỏ.

---

## ISSUE-04 — Restart ứng dụng chưa an toàn

### WPF

`RestartApp()` hiện vẫn shutdown ứng dụng khi:

- `Environment.ProcessPath` null hoặc rỗng.
- `Process.Start()` trả về null.

Điều này có thể biến thao tác Restart thành chỉ đóng ứng dụng. 

### Yêu cầu sửa WPF

- Chỉ gọi `Application.Current.Shutdown()` sau khi xác nhận process mới đã được tạo.
- Nếu không lấy được process path:
  - Hiển thị lỗi.
  - Giữ ứng dụng hiện tại chạy.
- Nếu `Process.Start()` trả về null:
  - Hiển thị lỗi.
  - Không shutdown.
- Log đầy đủ exception.

### Wasm

`InvokeVoidAsync("location.reload")` hiện không được await. Exception bất đồng bộ không đi vào `catch`. 

### Yêu cầu sửa Wasm

Do interface hiện tại trả về `void`, dùng async helper:

```csharp
public void RestartApp()
{
    _ = RestartAsync();
}

private async Task RestartAsync()
{
    try
    {
        await _js.InvokeVoidAsync("location.reload");
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to restart web application");
    }
}
```

Không dùng `.Wait()` hoặc `.GetAwaiter().GetResult()`.

---

# P2 — Cần sửa trước lần review tiếp theo

## ISSUE-05 — `LanguageSelector` không thực sự implement `IDisposable`

Component có method `Dispose()` nhưng thiếu:

```razor
@implements IDisposable
```

Do đó Blazor không đảm bảo gọi `Dispose()` và subscription `Lang.LanguageChanged` có thể không được tháo. 

### Yêu cầu sửa

Thêm ở đầu component:

```razor
@implements IDisposable
```

Không thay đổi behavior khác của component.

---

## ISSUE-06 — ModuleLoader cache key và API lookup không khớp

Cả WPF và Wasm lưu cache bằng key:

```csharp
$"{menuItem.Assembly}|{menuItem.Component}"
```

nhưng `GetCachedType(string componentName)` vẫn lookup trực tiếp bằng `componentName`. Method này vì thế gần như luôn trả `null`.  

### Yêu cầu sửa

Chọn một thiết kế nhất quán, ưu tiên:

```csharp
Type? GetCachedType(string assemblyName, string componentName);
```

Dùng chung helper tạo cache key trong cả `LoadComponentAsync()` và `GetCachedType()`.

Ví dụ:

```csharp
private static string CreateCacheKey(string assemblyName, string componentName)
    => $"{assemblyName}|{componentName}";
```

Cập nhật cả:

- `IModuleLoader`
- WPF `ModuleLoader`
- Wasm `ModuleLoader`
- Call site liên quan.

Nếu method không còn được sử dụng, có thể xóa khỏi contract và hai implementation, nhưng phải xác nhận không có call site trong toàn solution.

---

## ISSUE-07 — ErrorBoundary tự recover ở mọi parent render

`PageContainer.OnParametersSet()` luôn gọi:

```csharp
_errorBoundary?.Recover();
```

`OnParametersSet()` có thể chạy lại dù `Type` không đổi, ví dụ khi layout render do toggle menu, toggle header hoặc state khác. Page đang lỗi có thể tự động được tạo lại và throw lần nữa. 

### Yêu cầu sửa

- Không gọi `Recover()` vô điều kiện trong `OnParametersSet()`.
- Với việc đổi `@key` thành `active`, khi navigation instance thay đổi thì `PageContainer` mới sẽ được tạo; không cần auto recover.
- Chỉ gọi `Recover()` khi người dùng bấm Retry.
- `OnBack()` phải trả về `Task` và await navigation:

```csharp
private Task OnBack() => Navigator.BackAsync();
```

- Xóa `NavigationManager` nếu không dùng.
- Không thêm cơ chế retry tự động hoặc timer.

---

## ISSUE-08 — Web build module hai lần

`MAP.H.Web.csproj` vừa khai báo module dưới dạng `ProjectReference`, vừa gọi `MSBuild Targets="Build"` lần nữa trong `BuildAndSyncModules`.

Điều này làm build phức tạp hơn và có thể build module lặp lại. 

### Yêu cầu sửa

Giữ một đường build duy nhất:

- `ProjectReference` chịu trách nhiệm build module.
- Target tùy chỉnh chỉ lấy output và copy DLL/PDB nếu thực sự cần.
- Không gọi `Targets="Build"` lần hai cho cùng module.
- Giữ nguyên lazy-load behavior.
- Không xóa PDB khỏi thư mục module.
- Không thay đổi source project module.

Sau khi sửa phải xác nhận:

- Build Debug thành công.
- Build Release thành công.
- Publish Web thành công.
- Các module DLL xuất hiện đúng vị trí lazy loading.
- Module có thể được tải ở runtime.

---

# Test và kiểm tra bắt buộc

Agent phải chạy:

```powershell
dotnet build MAP.slnx -c Debug
dotnet build MAP.slnx -c Release
dotnet test Tests/MAP.C.Runtime.Tests/MAP.C.Runtime.Tests.csproj -c Debug
dotnet publish MAP.H.Web/MAP.H.Web.csproj -c Release
dotnet publish MAP.H.Desktop/MAP.H.Desktop.csproj -c Release
```

Nếu có script build chính thức thì chạy thêm:

```powershell
.\build.ps1
.\build-all.ps1
```

Không chạy deploy thật đến server nếu chưa được yêu cầu.

## Báo cáo cuối cùng của agent

Agent phải trả lại:

1. Danh sách file đã sửa.
2. Tóm tắt từng issue đã xử lý.
3. Danh sách test mới.
4. Kết quả từng lệnh build/test/publish.
5. Warning còn tồn tại.
6. Nội dung nào chủ động không sửa.
7. Không commit và không push.

## Điều kiện hoàn thành

Chỉ xem là hoàn thành khi:

- Navigation không mất state nếu replace page thất bại.
- Mở lại cùng page với parameters mới tạo đúng page instance mới.
- Parameters nhạy cảm không xuất hiện trong log.
- Deploy không báo thành công khi thiếu output.
- Restart không đóng app nếu không tạo được process mới.
- Wasm reload xử lý được exception bất đồng bộ.
- `LanguageSelector` được dispose đúng.
- Cache API hoạt động nhất quán.
- ErrorBoundary không tự retry khi layout render.
- Module không bị build lặp.
- Toàn bộ build/test/publish bắt buộc thành công.
- Không chuyển config/log khỏi thư mục executable.
- Không thực hiện WP02.