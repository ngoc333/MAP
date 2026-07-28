using System.IO;
using System.Text.Json;
using MAP.C.Contract.Models;
using MAP.C.Contract.Menus;

namespace MAP.C.Wpf;

public class DesktopMenuService : IMenuService
{
    private PageConfig? _config;

    public List<MenuItem> Menus => _config?.Menus ?? new();
    public event Action? OnMenusLoaded;

    public Task LoadMenusAsync()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "page.json");
        using var stream = File.OpenRead(path);
        _config = JsonSerializer.Deserialize<PageConfig>(stream,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        OnMenusLoaded?.Invoke();
        return Task.CompletedTask;
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
