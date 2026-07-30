using System.Net.Http.Json;
using System.Diagnostics;
using MAP.C.Contract.Database;
using MAP.C.Contract.Models;
using MAP.C.Contract.Menus;
using MAP.C.Runtime.Database;
using MAP.C.Runtime.Menus;
using Microsoft.Extensions.Logging;

namespace MAP.C.Wasm.Menus;

public class MenuService : IMenuService
{
    private readonly HttpClient _http;
    private readonly IDbApiClient _dbClient;
    private readonly ILogger<MenuService> _logger;
    private PageConfig? _config;

    public List<MenuItem> Menus => _config?.Menus ?? new();

    public event Action? OnMenusLoaded;

    public MenuService(HttpClient http, IDbApiClient dbClient, ILogger<MenuService> logger)
    {
        _http = http;
        _dbClient = dbClient;
        _logger = logger;
    }

    public async Task LoadMenusAsync()
    {
        var started = Stopwatch.GetTimestamp();
        try
        {
            _config = await _http.GetFromJsonAsync<PageConfig>("page.json");
            if (_config is null)
                throw new InvalidOperationException("Menu configuration could not be loaded.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Local web menu load failed; using fallback menu.");
            _config = GetFallbackMenus();
            SystemMenus.EnsureRegistered(_config);
            OnMenusLoaded?.Invoke();
            return;
        }

        if (string.Equals(_config.Source, "db", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                _config = await DatabaseMenuLoader.LoadAsync(
                    _dbClient, _config.DbName!, _config.DbFunction!);
                _logger.LogInformation("Database menu loaded. MenuCount={MenuCount}", _config.Menus.Count);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Database menu load failed; preserving local menu.");
                // Preserve the local menu if the remote source is unavailable or invalid.
            }
        }

        SystemMenus.EnsureRegistered(_config);
        _logger.LogInformation("Web menu ready. MenuCount={MenuCount} DurationMs={DurationMs}", _config.Menus.Count, Stopwatch.GetElapsedTime(started).TotalMilliseconds);

        OnMenusLoaded?.Invoke();
    }

    public MenuItem? FindById(string id)
    {
        return MenuTree.Find(Menus, id);
    }

    private static PageConfig GetFallbackMenus()
    {
        return new PageConfig
        {
            Menus = new List<MenuItem>
            {
                new() { Id = "home", Title = "Trang chủ", Icon = "home",
                    Children = new List<MenuItem>
                    {
                        new() { Id = "fallback-dashboard", Title = "Dashboard", Icon = "dashboard" }
                    }
                }
            }
        };
    }
}
