using MAP.C.Contract.Models;

namespace MAP.C.Contract.Config;

public interface IAppConfigService
{
    bool Exists { get; }
    AppConfig? Current { get; }
    SystemInfo GetSystemInfo();
    IReadOnlyList<DisplayInfo> GetDisplays();
    Task SaveAsync(AppConfig config);
    void RestartApp();
}
