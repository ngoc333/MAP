using MAP.C.Contract.Menus;
using MAP.C.Contract.Models;

namespace MAP.C.Runtime.Tests;

public sealed class MenuConfigValidatorTests
{
    [Fact]
    public void Validate_RejectsMenuItemWithoutLocalizedTitle()
    {
        var config = new PageConfig { Menus = [new MenuItem { Id = "product-list" }] };

        var exception = Assert.Throws<InvalidOperationException>(() => MenuConfigValidator.Validate(config));

        Assert.Contains("product-list", exception.Message);
        Assert.Contains("localized title", exception.Message);
    }

    [Fact]
    public void Validate_RejectsPageGroupHybrid()
    {
        var config = new PageConfig
        {
            Menus =
            [
                new MenuItem
                {
                    Id = "products",
                    Titles = new Dictionary<string, string> { ["vi"] = "Sản phẩm" },
                    Assembly = "Products.dll",
                    Component = "Products.Page",
                    Children = [new MenuItem { Id = "product-list", Titles = new Dictionary<string, string> { ["vi"] = "Danh sách" } }]
                }
            ]
        };

        var exception = Assert.Throws<InvalidOperationException>(() => MenuConfigValidator.Validate(config));

        Assert.Contains("both a page and a group", exception.Message);
    }
}
