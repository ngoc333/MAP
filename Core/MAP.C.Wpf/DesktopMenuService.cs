using System.IO;
using System.Text.Json;
using System.Diagnostics;
using MAP.C.Contract.Database;
using MAP.C.Contract.Models;
using MAP.C.Contract.Menus;
using MAP.C.Runtime.Database;
using MAP.C.Runtime.Menus;
using Microsoft.Extensions.Logging;

namespace MAP.C.Wpf;

public class DesktopMenuService : IMenuService
{
    private readonly IDbApiClient _dbClient;
    private readonly ILogger<DesktopMenuService> _logger;
    private PageConfig? _config;

    public List<MenuItem> Menus => _config?.Menus ?? new();
    public event Action? OnMenusLoaded;

    public DesktopMenuService(IDbApiClient dbClient, ILogger<DesktopMenuService> logger)
    {
        _dbClient = dbClient;
        _logger = logger;
    }

    public async Task LoadMenusAsync()
    {
        var started = Stopwatch.GetTimestamp();
        var path = Path.Combine(AppContext.BaseDirectory, "page.json");
        _logger.LogInformation("Loading desktop menu. Path={Path} Exists={Exists}", path, File.Exists(path));
        using var stream = File.OpenRead(path);
        _config = JsonSerializer.Deserialize<PageConfig>(stream,
            new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidOperationException("Menu configuration could not be loaded.");

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
        _logger.LogInformation("Desktop menu ready. MenuCount={MenuCount} DurationMs={DurationMs}", _config.Menus.Count, Stopwatch.GetElapsedTime(started).TotalMilliseconds);

        OnMenusLoaded?.Invoke();
    }

    public MenuItem? FindById(string id)
    {
        foreach (var menu in Menus)
        {
            if (menu.Id == id) return menu;
            if (menu.Children is not null)
            {
                var found = FindInList(menu.Children, id);
                if (found is not null) return found;
            }
        }
        return null;
    }

    private static MenuItem? FindInList(IEnumerable<MenuItem> items, string id)
    {
        foreach (var item in items)
        {
            if (item.Id == id) return item;
            if (item.Children is not null && FindInList(item.Children, id) is { } found) return found;
        }
        return null;
    }
}
