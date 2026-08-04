using MAP.C.Contract.Models;

namespace MAP.C.Runtime.Menus;

public static class SystemMenus
{
    public const string SystemLogsPageId = "system-logs";

    public static void EnsureRegistered(PageConfig config)
    {
        if (MenuTree.Find(config.Menus, SystemLogsPageId) is not null) return;

        var system = config.Menus.FirstOrDefault(x => x.Id == "system");
        if (system is null)
        {
            system = new MenuItem { Id = "system", Title = "Hệ thống", Icon = "settings", Children = new List<MenuItem>() };
            config.Menus.Add(system);
        }

        system.Children ??= new List<MenuItem>();
        system.Children.Add(new MenuItem
        {
            Id = SystemLogsPageId,
            Title = "Nhật ký",
            Icon = "article",
            Assembly = "MAP.M.System.dll",
            Component = "MAP.M.System.Pages.SystemLogsPage"
        });
    }
}
