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

        var json = await _js.InvokeAsync<string?>("mapConfig.get");
        if (string.IsNullOrEmpty(json))
        {
            // No stored configuration — valid first-run state
            _current = null;
            _existsInStorage = false;
            _loaded = true;
            _logger.LogInformation("No app config found; first-run state.");
            return;
        }

        _current = JsonSerializer.Deserialize<AppConfig>(json, _jsonOptions)
            ?? throw new InvalidOperationException(
                "App configuration in storage deserialized to null.");
        _existsInStorage = true;
        _loaded = true;
    }

    public async Task SaveAsync(AppConfig config)
    {
        try
        {
            var json = JsonSerializer.Serialize(config, _jsonOptions);
            await _js.InvokeVoidAsync("mapConfig.set", json);
            _current = config;
            _existsInStorage = true;
        }
        catch (JSException ex)
        {
            _logger.LogError(ex, "JS interop failed during config save");
            throw;
        }
    }

    public SystemInfo GetSystemInfo() => new();

    public IReadOnlyList<DisplayInfo> GetDisplays()
    {
        return [new DisplayInfo(0, "Browser", true)];
    }

    public void RestartApp()
    {
        try
        {
            // Use synchronous in-process JS interop in Blazor WebAssembly
            if (_js is IJSInProcessRuntime jsInProcess)
            {
                jsInProcess.InvokeVoid("location.reload");
            }
            else
            {
                _logger.LogWarning("IJSInProcessRuntime not available; restart may not work.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restart web application");
        }
    }
}
