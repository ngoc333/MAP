using System.Net.Http.Json;
using MAP.C.Contract.Database;
using MAP.C.Contract.Models;
using MAP.C.Contract.Menus;
using MAP.C.Runtime.Database;

namespace MAP.C.Wasm.Menus;

public class MenuService : IMenuService
{
    private readonly HttpClient _http;
    private readonly IDbApiClient _dbClient;
    private PageConfig? _config;

    public List<MenuItem> Menus => _config?.Menus ?? new();

    public event Action? OnMenusLoaded;

    public MenuService(HttpClient http, IDbApiClient dbClient)
    {
        _http = http;
        _dbClient = dbClient;
    }

    public async Task LoadMenusAsync()
    {
        try
        {
            _config = await _http.GetFromJsonAsync<PageConfig>("page.json");
            if (_config is null)
                throw new InvalidOperationException("Menu configuration could not be loaded.");
        }
        catch
        {
            _config = GetFallbackMenus();
            OnMenusLoaded?.Invoke();
            return;
        }

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
        return FindInList(Menus, id);
    }

    private static MenuItem? FindInList(List<MenuItem> items, string id)
    {
        foreach (var item in items)
        {
            if (item.Id == id) return item;
            if (item.Children is not null)
            {
                var found = FindInList(item.Children, id);
                if (found is not null) return found;
            }
        }
        return null;
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
