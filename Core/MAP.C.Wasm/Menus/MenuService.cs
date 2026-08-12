using System.Net.Http.Json;
using System.Text.Json;
using System.Diagnostics;
using MAP.C.Contract.Config;
using MAP.C.Contract.Database;
using MAP.C.Contract.Models;
using MAP.C.Contract.Context;
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
    private readonly IClientContextService _context;
    private PageConfig? _config;

    public List<MenuItem> Menus => _config?.Menus ?? new();
    public string? StartPageId => _config?.StartPageId;
    public string? DbName => _config?.DbName;

    public event Action? OnMenusLoaded;

    public MenuService(HttpClient http, IDbApiClient dbClient, IAppConfigService? configService,
        IClientContextService context, ILogger<MenuService> logger)
    {
        _http = http;
        _dbClient = dbClient;
        _configService = configService;
        _context = context;
        _logger = logger;
    }

    public async Task LoadMenusAsync()
    {
        if (_config is not null) return;

        var started = Stopwatch.GetTimestamp();
        PageConfig localConfig;
        try
        {
            localConfig = await _http.GetFromJsonAsync<PageConfig>("page.json")
                ?? throw new InvalidOperationException("Menu configuration deserialized to null.");
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"Invalid menu configuration 'page.json': {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to load menu configuration 'page.json': {ex.Message}", ex);
        }

        _config = await MenuConfigResolver.ResolveAsync(
            localConfig, _configService, _dbClient, _logger, started,
            _context.Current);

        OnMenusLoaded?.Invoke();
    }

    public MenuItem? FindById(string id)
    {
        return MenuTree.Find(Menus, id);
    }
}
