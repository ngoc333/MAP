using MAP.C.Contract.Models;

namespace MAP.C.Runtime.Menus;

public static class SystemMenus
{
    public const string LogViewerPageId = "system-log-viewer";

    public static void EnsureRegistered(PageConfig config)
    {
        if (MenuTree.Find(config.Menus, LogViewerPageId) is not null) return;

        var system = config.Menus.FirstOrDefault(x => x.Id == "system");
        if (system is null)
        {
            system = new MenuItem { Id = "system", Title = "Hệ thống", Icon = "settings", Children = new List<MenuItem>() };
            config.Menus.Add(system);
        }

        system.Children ??= new List<MenuItem>();
        system.Children.Add(new MenuItem
        {
            Id = LogViewerPageId,
            Title = "Nhật ký",
            Icon = "article",
            Assembly = "MAP.M.LogViewer.dll",
            Component = "MAP.M.LogViewer.Pages.LogViewerPage"
        });
    }
}
