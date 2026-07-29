using System.IO;
using System.Text.Json;
using MAP.C.Contract.Database;
using MAP.C.Contract.Models;
using MAP.C.Contract.Menus;
using MAP.C.Runtime.Database;

namespace MAP.C.Wpf;

public class DesktopMenuService : IMenuService
{
    private readonly IDbApiClient _dbClient;
    private PageConfig? _config;

    public List<MenuItem> Menus => _config?.Menus ?? new();
    public event Action? OnMenusLoaded;

    public DesktopMenuService(IDbApiClient dbClient)
    {
        _dbClient = dbClient;
    }

    public async Task LoadMenusAsync()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "page.json");
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
            }
            catch
            {
                // Preserve the local menu if the remote source is unavailable or invalid.
            }
        }

        OnMenusLoaded?.Invoke();
    }

    public MenuItem? FindById(string id)
    {
        foreach (var menu in Menus)
        {
            if (menu.Id == id) return menu;
            if (menu.Children is not null)
            {
                var found = menu.Children.FirstOrDefault(c => c.Id == id);
                if (found is not null) return found;
            }
        }
        return null;
    }
}
