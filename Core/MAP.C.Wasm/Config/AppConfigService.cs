using System.Text.Json;
using MAP.C.Contract.Config;
using MAP.C.Contract.Models;
using Microsoft.JSInterop;

namespace MAP.C.Wasm.Config;

public sealed class AppConfigService : IAppConfigService
{
    private readonly IJSRuntime _js;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);
    private AppConfig? _current;
    private bool _loaded;

    public AppConfigService(IJSRuntime js)
    {
        _js = js;
    }

    public bool Exists => _current is not null;

    public AppConfig? Current => _current;

    public async Task LoadAsync()
    {
        if (_loaded) return;
        _loaded = true;

        try
        {
            var json = await _js.InvokeAsync<string?>("mapConfig.get");
            if (!string.IsNullOrEmpty(json))
            {
                _current = JsonSerializer.Deserialize<AppConfig>(json, _jsonOptions);
            }

            if (_current is null)
            {
                _current = new AppConfig();
                var defaultJson = JsonSerializer.Serialize(_current, _jsonOptions);
                await _js.InvokeVoidAsync("mapConfig.set", defaultJson);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AppConfigService] Load failed: {ex}");
            _current ??= new AppConfig();
        }
    }

    public async Task SaveAsync(AppConfig config)
    {
        var json = JsonSerializer.Serialize(config, _jsonOptions);
        await _js.InvokeVoidAsync("mapConfig.set", json);
        _current = config;
    }

    public SystemInfo GetSystemInfo() => new();

    public IReadOnlyList<DisplayInfo> GetDisplays()
    {
        return [new DisplayInfo(0, "Browser", true)];
    }

    public void RestartApp()
    {
        _ = _js.InvokeVoidAsync("location.reload");
    }
}
