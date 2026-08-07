using System.Net.Http.Json;
using System.Diagnostics;
using MAP.C.Contract.Config;
using MAP.C.Contract.Database;
using MAP.C.Contract.Models;
using MAP.C.Contract.Menus;
using MAP.C.Runtime.Menus;
using Microsoft.Extensions.Logging;

namespace MAP.C.Wasm.Menus;

public class MenuService : IMenuService
{
    private readonly HttpClient _http;
    private readonly IDbApiClient _dbClient;
    private readonly IAppConfigService? _configService;
    private readonly ILogger<MenuService> _logger;
    private PageConfig? _config;

    public List<MenuItem> Menus => _config?.Menus ?? new();

    public event Action? OnMenusLoaded;

    public MenuService(HttpClient http, IDbApiClient dbClient, IAppConfigService? configService, ILogger<MenuService> logger)
    {
        _http = http;
        _dbClient = dbClient;
        _configService = configService;
        _logger = logger;
    }

    public async Task LoadMenusAsync()
    {
        var started = Stopwatch.GetTimestamp();
        PageConfig localConfig;
        try
        {
            localConfig = await _http.GetFromJsonAsync<PageConfig>("page.json")
                ?? throw new InvalidOperationException("Menu configuration could not be loaded.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Local web menu load failed; using fallback menu.");
            localConfig = GetFallbackMenus();
        }

        _config = await MenuConfigResolver.ResolveAsync(
            localConfig, _configService, _dbClient, _logger, started);

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
