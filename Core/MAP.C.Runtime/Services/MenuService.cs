using System.Net.Http.Json;
using MAP.C.Contract.Models;
using MAP.C.Contract.Services;

namespace MAP.C.Runtime.Services;

public class MenuService : IMenuService
{
    private readonly HttpClient _http;
    private PageConfig? _config;

    public List<MenuItem> Menus => _config?.Menus ?? new();

    public event Action? OnMenusLoaded;

    public MenuService(HttpClient http)
    {
        _http = http;
    }

    public async Task LoadMenusAsync()
    {
        try
        {
            _config = await _http.GetFromJsonAsync<PageConfig>("page.json");
        }
        catch
        {
            _config = GetFallbackMenus();
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
