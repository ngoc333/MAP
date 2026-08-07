using MAP.C.Contract.Models;
using MAP.C.Runtime.Menus;

namespace MAP.C.Runtime.Tests;

public class MenuTitleKeyFallbackTests
{
    // TEST01: Local has TitleKey, DB has null → resolved TitleKey comes from local
    [Fact]
    public void FillMissingTitleKeys_LocalHasKey_DbNull_CopiesFromLocal()
    {
        var local = new List<MenuItem>
        {
            new() { Id = "products", Title = "Sản phẩm", TitleKey = "menu.products" }
        };
        var db = new List<MenuItem>
        {
            new() { Id = "products", Title = "Sản phẩm" }
        };

        MenuConfigResolver.FillMissingTitleKeys(db, local);

        Assert.Equal("menu.products", db[0].TitleKey);
    }

    // TEST02: DB has its own TitleKey → preserved, not overwritten
    [Fact]
    public void FillMissingTitleKeys_DbHasKey_PreservesDbKey()
    {
        var local = new List<MenuItem>
        {
            new() { Id = "products", Title = "Sản phẩm", TitleKey = "menu.products" }
        };
        var db = new List<MenuItem>
        {
            new() { Id = "products", Title = "Sản phẩm", TitleKey = "custom.products" }
        };

        MenuConfigResolver.FillMissingTitleKeys(db, local);

        Assert.Equal("custom.products", db[0].TitleKey);
    }

    // TEST03: Nested child TitleKey merged recursively
    [Fact]
    public void FillMissingTitleKeys_NestedChild_MergesRecursively()
    {
        var local = new List<MenuItem>
        {
            new()
            {
                Id = "products", Title = "Sản phẩm", TitleKey = "menu.products",
                Children =
                [
                    new() { Id = "product-list", Title = "Danh sách", TitleKey = "menu.productList" }
                ]
            }
        };
        var db = new List<MenuItem>
        {
            new()
            {
                Id = "products", Title = "Sản phẩm",
                Children =
                [
                    new() { Id = "product-list", Title = "Danh sách" }
                ]
            }
        };

        MenuConfigResolver.FillMissingTitleKeys(db, local);

        Assert.Equal("menu.products", db[0].TitleKey);
        Assert.Equal("menu.productList", db[0].Children![0].TitleKey);
    }

    // TEST04: Unknown DB menu with no local counterpart → TitleKey remains null
    [Fact]
    public void FillMissingTitleKeys_UnknownMenu_RemainsNull()
    {
        var local = new List<MenuItem>();
        var db = new List<MenuItem>
        {
            new() { Id = "unknown", Title = "Unknown" }
        };

        MenuConfigResolver.FillMissingTitleKeys(db, local);

        Assert.Null(db[0].TitleKey);
    }
}

public class SystemMenusTests
{
    // TEST05: New system menus get TitleKey
    [Fact]
    public void EnsureRegistered_NewMenus_SetsTitleKey()
    {
        var config = new PageConfig { Menus = [] };

        SystemMenus.EnsureRegistered(config);

        var system = config.Menus.First(x => x.Id == "system");
        Assert.Equal("menu.system", system.TitleKey);

        var systemConfig = system.Children!.First(x => x.Id == SystemMenus.SystemConfigPageId);
        Assert.Equal("menu.systemConfig", systemConfig.TitleKey);

        var systemLogs = system.Children.First(x => x.Id == SystemMenus.SystemLogsPageId);
        Assert.Equal("menu.systemLogs", systemLogs.TitleKey);
    }

    // TEST06: Existing system menus from DB get TitleKey filled
    [Fact]
    public void EnsureRegistered_ExistingMenus_FillsMissingTitleKey()
    {
        var config = new PageConfig
        {
            Menus =
            [
                new MenuItem
                {
                    Id = "system", Title = "System",
                    Children =
                    [
                        new() { Id = SystemMenus.SystemConfigPageId, Title = "Config" },
                        new() { Id = SystemMenus.SystemLogsPageId, Title = "Logs" }
                    ]
                }
            ]
        };

        SystemMenus.EnsureRegistered(config);

        var system = config.Menus.First(x => x.Id == "system");
        Assert.Equal("menu.system", system.TitleKey);

        var systemConfig = system.Children!.First(x => x.Id == SystemMenus.SystemConfigPageId);
        Assert.Equal("menu.systemConfig", systemConfig.TitleKey);

        var systemLogs = system.Children.First(x => x.Id == SystemMenus.SystemLogsPageId);
        Assert.Equal("menu.systemLogs", systemLogs.TitleKey);
    }

    // TEST07: DB TitleKey is never overwritten
    [Fact]
    public void EnsureRegistered_ExistingMenus_DoesNotOverwriteTitleKey()
    {
        var config = new PageConfig
        {
            Menus =
            [
                new MenuItem
                {
                    Id = "system", Title = "System", TitleKey = "custom.system",
                    Children =
                    [
                        new() { Id = SystemMenus.SystemConfigPageId, Title = "Config", TitleKey = "custom.config" },
                        new() { Id = SystemMenus.SystemLogsPageId, Title = "Logs", TitleKey = "custom.logs" }
                    ]
                }
            ]
        };

        SystemMenus.EnsureRegistered(config);

        var system = config.Menus.First(x => x.Id == "system");
        Assert.Equal("custom.system", system.TitleKey);

        var systemConfig = system.Children!.First(x => x.Id == SystemMenus.SystemConfigPageId);
        Assert.Equal("custom.config", systemConfig.TitleKey);

        var systemLogs = system.Children.First(x => x.Id == SystemMenus.SystemLogsPageId);
        Assert.Equal("custom.logs", systemLogs.TitleKey);
    }
}
