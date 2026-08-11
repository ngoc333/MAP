using MAP.C.Contract.Menus;
using MAP.C.Contract.Models;

namespace MAP.C.Runtime.Tests;

public sealed class MenuConfigValidatorTests
{
    // Valid nested menu: group -> group -> page
    [Fact]
    public void Validate_ValidNestedMenu_Passes()
    {
        var config = new PageConfig
        {
            Menus =
            [
                new MenuItem
                {
                    Id = "products",
                    Titles = new Dictionary<string, string> { ["vi"] = "Sản phẩm", ["en"] = "Products" },
                    Children =
                    [
                        new MenuItem
                        {
                            Id = "product-management",
                            Titles = new Dictionary<string, string> { ["vi"] = "Quản lý", ["en"] = "Management" },
                            Children =
                            [
                                new MenuItem
                                {
                                    Id = "product-list",
                                    Titles = new Dictionary<string, string> { ["vi"] = "Danh sách", ["en"] = "List" },
                                    Assembly = "Products.dll",
                                    Component = "Products.ProductList"
                                }
                            ]
                        }
                    ]
                }
            ]
        };

        MenuConfigValidator.Validate(config);
    }

    // Missing ID
    [Fact]
    public void Validate_MissingId_ThrowsInvalidOperationException()
    {
        var config = new PageConfig
        {
            Menus = [new MenuItem { Id = "", Titles = new Dictionary<string, string> { ["vi"] = "Test" } }]
        };

        var exception = Assert.Throws<InvalidOperationException>(() => MenuConfigValidator.Validate(config));
        Assert.Contains("id must not be empty", exception.Message);
    }

    // Missing localized title (all empty)
    [Fact]
    public void Validate_AllEmptyTitles_ThrowsInvalidOperationException()
    {
        var config = new PageConfig
        {
            Menus =
            [
                new MenuItem
                {
                    Id = "product-list",
                    Titles = new Dictionary<string, string> { ["vi"] = "", ["en"] = "  " },
                    Assembly = "Test.dll",
                    Component = "Test.Page"
                }
            ]
        };

        var exception = Assert.Throws<InvalidOperationException>(() => MenuConfigValidator.Validate(config));
        Assert.Contains("product-list", exception.Message);
        Assert.Contains("localized title", exception.Message);
    }

    // Null Titles
    [Fact]
    public void Validate_NullTitles_ThrowsInvalidOperationException()
    {
        var config = new PageConfig
        {
            Menus =
            [
                new MenuItem
                {
                    Id = "product-list",
                    Titles = null!,
                    Assembly = "Test.dll",
                    Component = "Test.Page"
                }
            ]
        };

        var exception = Assert.Throws<InvalidOperationException>(() => MenuConfigValidator.Validate(config));
        Assert.Contains("product-list", exception.Message);
        Assert.Contains("titles collection", exception.Message);
    }

    // Null Children
    [Fact]
    public void Validate_NullChildren_ThrowsInvalidOperationException()
    {
        var config = new PageConfig
        {
            Menus =
            [
                new MenuItem
                {
                    Id = "products",
                    Titles = new Dictionary<string, string> { ["vi"] = "Sản phẩm" },
                    Children = null!
                }
            ]
        };

        var exception = Assert.Throws<InvalidOperationException>(() => MenuConfigValidator.Validate(config));
        Assert.Contains("products", exception.Message);
        Assert.Contains("children collection", exception.Message);
    }

    // Null Menus collection
    [Fact]
    public void Validate_NullMenus_ThrowsInvalidOperationException()
    {
        var config = new PageConfig { Menus = null! };

        var exception = Assert.Throws<InvalidOperationException>(() => MenuConfigValidator.Validate(config));
        Assert.Contains("menu collection", exception.Message);
    }

    // Partial page definition - Assembly only
    [Fact]
    public void Validate_AssemblyOnly_ThrowsInvalidOperationException()
    {
        var config = new PageConfig
        {
            Menus =
            [
                new MenuItem
                {
                    Id = "product-list",
                    Titles = new Dictionary<string, string> { ["vi"] = "Danh sách" },
                    Assembly = "Products.dll"
                }
            ]
        };

        var exception = Assert.Throws<InvalidOperationException>(() => MenuConfigValidator.Validate(config));
        Assert.Contains("product-list", exception.Message);
        Assert.Contains("both Assembly and Component", exception.Message);
    }

    // Partial page definition - Component only
    [Fact]
    public void Validate_ComponentOnly_ThrowsInvalidOperationException()
    {
        var config = new PageConfig
        {
            Menus =
            [
                new MenuItem
                {
                    Id = "product-list",
                    Titles = new Dictionary<string, string> { ["vi"] = "Danh sách" },
                    Component = "Products.ProductList"
                }
            ]
        };

        var exception = Assert.Throws<InvalidOperationException>(() => MenuConfigValidator.Validate(config));
        Assert.Contains("product-list", exception.Message);
        Assert.Contains("both Assembly and Component", exception.Message);
    }

    // Page + Group hybrid
    [Fact]
    public void Validate_PageGroupHybrid_ThrowsInvalidOperationException()
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
                    Children = [new MenuItem { Id = "child", Titles = new Dictionary<string, string> { ["vi"] = "Child" } }]
                }
            ]
        };

        var exception = Assert.Throws<InvalidOperationException>(() => MenuConfigValidator.Validate(config));
        Assert.Contains("products", exception.Message);
        Assert.Contains("both a page and a group", exception.Message);
    }

    // Orphan node - not a page, not a group
    [Fact]
    public void Validate_OrphanNode_ThrowsInvalidOperationException()
    {
        var config = new PageConfig
        {
            Menus =
            [
                new MenuItem
                {
                    Id = "orphan",
                    Titles = new Dictionary<string, string> { ["vi"] = "Orphan" }
                }
            ]
        };

        var exception = Assert.Throws<InvalidOperationException>(() => MenuConfigValidator.Validate(config));
        Assert.Contains("orphan", exception.Message);
        Assert.Contains("must be either a page or a group", exception.Message);
    }

    // Duplicate root IDs
    [Fact]
    public void Validate_DuplicateRootIds_ThrowsInvalidOperationException()
    {
        var config = new PageConfig
        {
            Menus =
            [
                new MenuItem
                {
                    Id = "products",
                    Titles = new Dictionary<string, string> { ["vi"] = "Products" },
                    Assembly = "Test.dll",
                    Component = "Test.Page1"
                },
                new MenuItem
                {
                    Id = "products",
                    Titles = new Dictionary<string, string> { ["vi"] = "Products 2" },
                    Assembly = "Test.dll",
                    Component = "Test.Page2"
                }
            ]
        };

        var exception = Assert.Throws<InvalidOperationException>(() => MenuConfigValidator.Validate(config));
        Assert.Contains("products", exception.Message);
        Assert.Contains("Duplicate", exception.Message);
    }

    // Duplicate IDs at different levels (global uniqueness)
    [Fact]
    public void Validate_DuplicateIdsAtDifferentLevels_ThrowsInvalidOperationException()
    {
        var config = new PageConfig
        {
            Menus =
            [
                new MenuItem
                {
                    Id = "products",
                    Titles = new Dictionary<string, string> { ["vi"] = "Products" },
                    Children =
                    [
                        new MenuItem
                        {
                            Id = "product-list",
                            Titles = new Dictionary<string, string> { ["vi"] = "List" },
                            Assembly = "Test.dll",
                            Component = "Test.ProductList"
                        }
                    ]
                },
                new MenuItem
                {
                    Id = "customers",
                    Titles = new Dictionary<string, string> { ["vi"] = "Customers" },
                    Children =
                    [
                        new MenuItem
                        {
                            Id = "product-list",
                            Titles = new Dictionary<string, string> { ["vi"] = "List" },
                            Assembly = "Test.dll",
                            Component = "Test.CustomerList"
                        }
                    ]
                }
            ]
        };

        var exception = Assert.Throws<InvalidOperationException>(() => MenuConfigValidator.Validate(config));
        Assert.Contains("product-list", exception.Message);
        Assert.Contains("Duplicate", exception.Message);
    }

    // Null menu item in list
    [Fact]
    public void Validate_NullMenuItem_ThrowsInvalidOperationException()
    {
        var config = new PageConfig
        {
            Menus = [null!]
        };

        var exception = Assert.Throws<InvalidOperationException>(() => MenuConfigValidator.Validate(config));
        Assert.Contains("null menu item", exception.Message);
    }

    // Existing test: Rejects menu item without localized title
    [Fact]
    public void Validate_RejectsMenuItemWithoutLocalizedTitle()
    {
        var config = new PageConfig { Menus = [new MenuItem { Id = "product-list" }] };

        var exception = Assert.Throws<InvalidOperationException>(() => MenuConfigValidator.Validate(config));

        Assert.Contains("product-list", exception.Message);
        Assert.Contains("localized title", exception.Message);
    }

    // Existing test: Rejects page-group hybrid
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
