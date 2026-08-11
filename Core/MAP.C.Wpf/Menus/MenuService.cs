using System.IO;
using System.Text.Json;
using System.Diagnostics;
using MAP.C.Contract.Config;
using MAP.C.Contract.Database;
using MAP.C.Contract.Models;
using MAP.C.Contract.Menus;
using MAP.C.Runtime.Menus;
using Microsoft.Extensions.Logging;

namespace MAP.C.Wpf.Menus;

public sealed class MenuService : IMenuService
{
    private readonly IDbApiClient _dbClient;
    private readonly IAppConfigService _configService;
    private readonly ILogger<MenuService> _logger;
    private PageConfig? _config;

    public List<MenuItem> Menus => _config?.Menus ?? new();
    public event Action? OnMenusLoaded;

    public MenuService(IDbApiClient dbClient, IAppConfigService configService, ILogger<MenuService> logger)
    {
        _dbClient = dbClient;
        _configService = configService;
        _logger = logger;
    }

    public async Task LoadMenusAsync()
    {
        if (_config is not null) return;

        var started = Stopwatch.GetTimestamp();
        var path = Path.Combine(AppContext.BaseDirectory, "page.json");
        _logger.LogDebug("Loading WPF menu. Path={Path} Exists={Exists}", path, File.Exists(path));

        PageConfig localConfig;
        try
        {
            using var stream = File.OpenRead(path);
            localConfig = JsonSerializer.Deserialize<PageConfig>(stream,
                new JsonSerializerOptions(JsonSerializerDefaults.Web))
                ?? throw new InvalidOperationException("Menu configuration deserialized to null.");
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"Invalid menu configuration '{path}': {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to load menu configuration '{path}': {ex.Message}", ex);
        }

        _config = await MenuConfigResolver.ResolveAsync(
            localConfig, _configService, _dbClient, _logger, started);

        OnMenusLoaded?.Invoke();
    }

    public MenuItem? FindById(string id) => MenuTree.Find(Menus, id);
}
