using System.Text.Json;
using MAP.C.Contract.Config;
using MAP.C.Contract.Models;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace MAP.C.Wasm.Config;

public sealed class AppConfigService : IAppConfigService
{
    private readonly IJSRuntime _js;
    private readonly ILogger<AppConfigService> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);
    private AppConfig? _current;
    private bool _loaded;
    private bool _existsInStorage;

    public AppConfigService(IJSRuntime js, ILogger<AppConfigService> logger)
    {
        _js = js;
        _logger = logger;
    }

    public bool Exists => _existsInStorage;

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
                _existsInStorage = _current is not null;
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
            _logger.LogError(ex, "Failed to load app config");
            _current ??= new AppConfig();
        }
    }

    public async Task SaveAsync(AppConfig config)
    {
        var json = JsonSerializer.Serialize(config, _jsonOptions);
        await _js.InvokeVoidAsync("mapConfig.set", json);
        _current = config;
        _existsInStorage = true;
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
