using MAP.C.Contract.Models;

namespace MAP.C.Runtime.Menus;

public static class SystemMenus
{
    public const string SystemLogsPageId = "system-logs";
    public const string SystemConfigPageId = "system-config";

    public static void EnsureRegistered(PageConfig config)
    {
        var system = config.Menus.FirstOrDefault(x => x.Id == "system");
        if (system is null)
        {
            system = new MenuItem { Id = "system", Title = "Hệ thống", TitleKey = "menu.system", Icon = "settings", Children = new List<MenuItem>() };
            config.Menus.Add(system);
        }
        else
        {
            system.TitleKey ??= "menu.system";
        }

        system.Children ??= new List<MenuItem>();

        var systemConfig = MenuTree.Find(config.Menus, SystemConfigPageId);
        if (systemConfig is null)
        {
            system.Children.Add(new MenuItem
            {
                Id = SystemConfigPageId,
                Title = "Cấu hình",
                TitleKey = "menu.systemConfig",
                Icon = "settings",
                Assembly = "MAP.M.System.dll",
                Component = "MAP.M.System.Pages.AppConfigPage"
            });
        }
        else
        {
            systemConfig.TitleKey ??= "menu.systemConfig";
        }

        var systemLogs = MenuTree.Find(config.Menus, SystemLogsPageId);
        if (systemLogs is null)
        {
            system.Children.Add(new MenuItem
            {
                Id = SystemLogsPageId,
                Title = "Nhật ký",
                TitleKey = "menu.systemLogs",
                Icon = "article",
                Assembly = "MAP.M.System.dll",
                Component = "MAP.M.System.Pages.SystemLogsPage"
            });
        }
        else
        {
            systemLogs.TitleKey ??= "menu.systemLogs";
        }
    }
}
