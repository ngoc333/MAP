using System.Text.Json;
using MAP.C.Contract.Database;
using MAP.C.Contract.Menus;
using MAP.C.Contract.Models;
using MAP.C.Runtime.Menus;

namespace MAP.C.Runtime.Tests;

public class MenuTitleContractTests
{
    // TEST01: Current language title is returned
    [Fact]
    public void Get_CurrentLanguage_ReturnsTitle()
    {
        var item = new MenuItem
        {
            Id = "product-list",
            Titles = new Dictionary<string, string>
            {
                ["vi"] = "Danh sách",
                ["en"] = "List"
            }
        };

        var result = MenuTitle.Get(item, "en");

        Assert.Equal("List", result);
    }

    // TEST02: Fallback to "vi" when current language not found
    [Fact]
    public void Get_CurrentLanguageNotAvailable_FallbackToVi()
    {
        var item = new MenuItem
        {
            Id = "product-list",
            Titles = new Dictionary<string, string>
            {
                ["vi"] = "Danh sách",
                ["en"] = "List"
            }
        };

        var result = MenuTitle.Get(item, "fr");

        Assert.Equal("Danh sách", result);
    }

    // TEST03: Fallback to first available non-empty title
    [Fact]
    public void Get_ViNotAvailable_FallbackToFirstAvailable()
    {
        var item = new MenuItem
        {
            Id = "product-list",
            Titles = new Dictionary<string, string>
            {
                ["en"] = "List"
            }
        };

        var result = MenuTitle.Get(item, "fr");

        Assert.Equal("List", result);
    }

    // TEST04: Fallback to menu id when no titles available
    [Fact]
    public void Get_NoTitles_FallbackToId()
    {
        var item = new MenuItem
        {
            Id = "product-list",
            Titles = new Dictionary<string, string>()
        };

        var result = MenuTitle.Get(item, "vi");

        Assert.Equal("product-list", result);
    }

    // TEST05: Fallback to menu id when all titles are empty
    [Fact]
    public void Get_AllTitlesEmpty_FallbackToId()
    {
        var item = new MenuItem
        {
            Id = "product-list",
            Titles = new Dictionary<string, string>
            {
                ["vi"] = "",
                ["en"] = "  "
            }
        };

        var result = MenuTitle.Get(item, "vi");

        Assert.Equal("product-list", result);
    }

    // TEST06: Custom default language
    [Fact]
    public void Get_CustomDefaultLanguage_UsesCustomDefault()
    {
        var item = new MenuItem
        {
            Id = "product-list",
            Titles = new Dictionary<string, string>
            {
                ["fr"] = "Liste",
                ["en"] = "List"
            }
        };

        var result = MenuTitle.Get(item, "de", "fr");

        Assert.Equal("Liste", result);
    }
}

public class MenuTreeContractTests
{
    // TEST07: Root navigable page
    [Fact]
    public void FindFirstPage_RootPage_ReturnsFirstPage()
    {
        var menus = new List<MenuItem>
        {
            new() { Id = "page1", Assembly = "Test.dll", Component = "Test.Page1" },
            new() { Id = "page2", Assembly = "Test.dll", Component = "Test.Page2" }
        };

        var result = MenuTree.FindFirstPage(menus);

        Assert.NotNull(result);
        Assert.Equal("page1", result.Id);
    }

    // TEST08: Nested page depth-first
    [Fact]
    public void FindFirstPage_NestedPage_ReturnsFirstNestedPage()
    {
        var menus = new List<MenuItem>
        {
            new()
            {
                Id = "parent",
                Children =
                [
                    new() { Id = "child-page", Assembly = "Test.dll", Component = "Test.ChildPage" }
                ]
            }
        };

        var result = MenuTree.FindFirstPage(menus);

        Assert.NotNull(result);
        Assert.Equal("child-page", result.Id);
    }

    // TEST09: List order controls first page
    [Fact]
    public void FindFirstPage_MultiplePages_ReturnsFirstInOrder()
    {
        var menus = new List<MenuItem>
        {
            new() { Id = "second", Assembly = "Test.dll", Component = "Test.Second" },
            new() { Id = "first", Assembly = "Test.dll", Component = "Test.First" }
        };

        var result = MenuTree.FindFirstPage(menus);

        Assert.NotNull(result);
        Assert.Equal("second", result.Id);
    }

    // TEST10: No navigable page returns null
    [Fact]
    public void FindFirstPage_NoNavigablePage_ReturnsNull()
    {
        var menus = new List<MenuItem>
        {
            new() { Id = "parent", Children = [] }
        };

        var result = MenuTree.FindFirstPage(menus);

        Assert.Null(result);
    }

    // TEST11: Empty list returns null
    [Fact]
    public void FindFirstPage_EmptyList_ReturnsNull()
    {
        var menus = new List<MenuItem>();

        var result = MenuTree.FindFirstPage(menus);

        Assert.Null(result);
    }
}

public class MenuItemContractTests
{
    // TEST12: Titles deserialization
    [Fact]
    public void MenuItem_Titles_DeserializesCorrectly()
    {
        var item = new MenuItem
        {
            Id = "test",
            Titles = new Dictionary<string, string>
            {
                ["vi"] = "Tiếng Việt",
                ["en"] = "English"
            }
        };

        Assert.Equal(2, item.Titles.Count);
        Assert.Equal("Tiếng Việt", item.Titles["vi"]);
        Assert.Equal("English", item.Titles["en"]);
    }

    // TEST13: HasChildren returns true when children exist
    [Fact]
    public void MenuItem_HasChildren_WithChildren_ReturnsTrue()
    {
        var item = new MenuItem
        {
            Id = "parent",
            Children = [new() { Id = "child" }]
        };

        Assert.True(item.HasChildren);
    }

    // TEST14: HasChildren returns false when no children
    [Fact]
    public void MenuItem_HasChildren_NoChildren_ReturnsFalse()
    {
        var item = new MenuItem { Id = "parent" };

        Assert.False(item.HasChildren);
    }

    // TEST15: IsPage returns true when Assembly and Component are set
    [Fact]
    public void MenuItem_IsPage_WithAssemblyAndComponent_ReturnsTrue()
    {
        var item = new MenuItem
        {
            Id = "page",
            Assembly = "Test.dll",
            Component = "Test.Page"
        };

        Assert.True(item.IsPage);
    }

    // TEST16: IsPage returns false when Assembly is missing
    [Fact]
    public void MenuItem_IsPage_MissingAssembly_ReturnsFalse()
    {
        var item = new MenuItem
        {
            Id = "page",
            Component = "Test.Page"
        };

        Assert.False(item.IsPage);
    }

    // TEST17: IsPage returns false when Component is missing
    [Fact]
    public void MenuItem_IsPage_MissingComponent_ReturnsFalse()
    {
        var item = new MenuItem
        {
            Id = "page",
            Assembly = "Test.dll"
        };

        Assert.False(item.IsPage);
    }
}

public class PostRefactorMenuConfigResolverTests
{
    // TEST18: Local mode uses local config
    [Fact]
    public async Task ResolveAsync_LocalMode_UsesLocalConfig()
    {
        var localConfig = new PageConfig
        {
            Source = "local",
            Menus = [new() { Id = "local-menu", Titles = new Dictionary<string, string> { ["vi"] = "Local" } }]
        };

        var result = await MenuConfigResolver.ResolveAsync(
            localConfig, null, null!, Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance, System.Diagnostics.Stopwatch.GetTimestamp());

        Assert.Single(result.Menus);
        Assert.Equal("local-menu", result.Menus[0].Id);
    }

    [Fact]
    public async Task ResolveAsync_DatabaseMode_UsesDatabaseConfig()
    {
        var localConfig = new PageConfig
        {
            Source = "db",
            DbName = "mes",
            DbFunction = "mes.fn_get_map_menu",
            Menus = [new() { Id = "local-menu" }]
        };
        var dbClient = new FakeDbApiClient(CreateMenuResponse("db-menu"));

        var result = await MenuConfigResolver.ResolveAsync(
            localConfig, null, dbClient,
            Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance,
            System.Diagnostics.Stopwatch.GetTimestamp());

        Assert.Single(result.Menus);
        Assert.Equal("db-menu", result.Menus[0].Id);
    }

    [Fact]
    public async Task ResolveAsync_DatabaseFailure_PropagatesWithoutLocalFallback()
    {
        var localConfig = new PageConfig
        {
            Source = "db",
            DbName = "mes",
            DbFunction = "mes.fn_get_map_menu",
            Menus = [new() { Id = "local-menu" }]
        };
        var dbClient = new FakeDbApiClient(new InvalidOperationException("database unavailable"));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            MenuConfigResolver.ResolveAsync(
                localConfig, null, dbClient,
                Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance,
                System.Diagnostics.Stopwatch.GetTimestamp()));

        Assert.Equal("database unavailable", exception.Message);
    }

    [Fact]
    public async Task ResolveAsync_UnknownSource_ThrowsConfigurationError()
    {
        var localConfig = new PageConfig
        {
            Source = "abc",
            Menus = [new() { Id = "local-menu" }]
        };

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            MenuConfigResolver.ResolveAsync(
                localConfig, null, null!,
                Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance,
                System.Diagnostics.Stopwatch.GetTimestamp()));

        Assert.Contains("Unsupported menu source", exception.Message);
        Assert.Contains("abc", exception.Message);
    }

    private static JsonElement CreateMenuResponse(string menuId)
    {
        var menu = JsonSerializer.Serialize(new PageConfig
        {
            Menus = [new() { Id = menuId }]
        });

        return JsonSerializer.SerializeToElement(new
        {
            success = true,
            data = new[]
            {
                new Dictionary<string, string> { ["fn_get_map_menu"] = menu }
            }
        });
    }

    private sealed class FakeDbApiClient : IDbApiClient
    {
        private readonly JsonElement? _response;
        private readonly Exception? _exception;

        public FakeDbApiClient(JsonElement response) => _response = response;
        public FakeDbApiClient(Exception exception) => _exception = exception;

        public Task<JsonElement> CallPostgreSqlFunctionAsync(
            string dbName, string commandName, JsonElement parameters,
            CancellationToken cancellationToken = default)
        {
            if (_exception is not null)
                return Task.FromException<JsonElement>(_exception);

            return Task.FromResult(_response!.Value);
        }

        public Task<JsonElement> CallOracleAsync(
            JsonElement requestBody, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<JsonElement> CallPostgreSqlProcedureAsync(
            string dbName, string commandName, JsonElement parameters,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
