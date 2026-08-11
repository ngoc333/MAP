using System.Text;
using MAP.C.Contract.Models;
using MAP.C.Contract.Navigation;
using MAP.C.Runtime.Database;
using MAP.C.Runtime.Menus;

namespace MAP.C.Runtime.Tests;

public sealed class RuntimeTests
{
    [Fact]
    public void PageParams_ConvertsAnonymousObject()
    {
        var parameters = PageParams.From(new { Code = "KH001", Count = 2 }, out var exception);

        Assert.Null(exception);
        Assert.NotNull(parameters);
        Assert.Equal("KH001", parameters["Code"]);
        Assert.Equal(2, parameters["Count"]);
    }

    [Fact]
    public void MenuTree_FindsNestedMenu()
    {
        var page = new MenuItem { Id = "page" };
        var menus = new List<MenuItem> { new() { Id = "root", Children = [page] } };

        Assert.Same(page, MenuTree.Find(menus, "page"));
    }

    [Fact]
    public void MenuTree_FindFirstPage_ReturnsFirstNavigablePage()
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

    [Fact]
    public void DbApiConfiguration_Load_ParsesAddresses()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("""
            { "OracleApiBaseUrl": "https://oracle.example/", "PostgreSqlApiBaseUrl": "https://postgres.example/" }
            """));

        var configuration = DbApiConfiguration.Load(stream);

        Assert.Equal(new Uri("https://oracle.example/"), configuration.OracleBaseAddress);
        Assert.Equal(new Uri("https://postgres.example/"), configuration.PostgreSqlBaseAddress);
    }

    [Fact]
    public async Task DbApiConfiguration_LoadAsync_ParsesAddresses()
    {
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("""
            { "OracleApiBaseUrl": "https://oracle.example/", "PostgreSqlApiBaseUrl": "https://postgres.example/" }
            """));

        var configuration = await DbApiConfiguration.LoadAsync(stream);

        Assert.Equal(new Uri("https://oracle.example/"), configuration.OracleBaseAddress);
        Assert.Equal(new Uri("https://postgres.example/"), configuration.PostgreSqlBaseAddress);
    }
}
