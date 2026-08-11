using MAP.C.Contract.Models;
using MAP.C.Contract.Navigation;
using MAP.C.UI.Headers;

namespace MAP.C.Runtime.Tests;

public sealed class PageHeaderResolverTests
{
    [Fact]
    public void ResolveTitle_UsesVietnameseMenuTitle_WhenThereIsNoPageHeader()
    {
        var page = CreatePage("product-list", "Danh sách", "List");

        var title = Resolve(page, null, "vi");

        Assert.Equal("Danh sách", title);
    }

    [Fact]
    public void ResolveTitle_UsesEnglishMenuTitle_WhenThereIsNoPageHeader()
    {
        var page = CreatePage("product-list", "Danh sách", "List");

        var title = Resolve(page, null, "en");

        Assert.Equal("List", title);
    }

    [Fact]
    public void ResolveTitle_MatchingPageOverrideWins()
    {
        var page = CreatePage("product-list", "Danh sách", "List");
        var header = new PageHeader("product-list", HeaderKind.Default, "ProductList.headerTitle");

        var title = Resolve(page, header, "vi");

        Assert.Equal("Danh sách sản phẩm", title);
    }

    [Fact]
    public void ResolveTitle_IgnoresStaleHeaderFromAnotherPage()
    {
        var customerPage = CreatePage("customer-list", "Danh sách khách hàng", "Customer List");
        var staleHeader = new PageHeader("product-list", HeaderKind.Default, "ProductList.headerTitle");

        var title = Resolve(customerPage, staleHeader, "vi");

        Assert.Null(PageHeaderResolver.GetMatchingHeader(customerPage, staleHeader));
        Assert.Equal("Danh sách khách hàng", title);
    }

    [Fact]
    public void ResolveTitle_UpdatesMenuAndOverrideTitlesForLanguageChange()
    {
        var page = CreatePage("product-list", "Danh sách", "List");
        var overrideHeader = new PageHeader("product-list", HeaderKind.Default, "ProductList.headerTitle");

        Assert.Equal("Danh sách", Resolve(page, null, "vi"));
        Assert.Equal("List", Resolve(page, null, "en"));
        Assert.Equal("Danh sách sản phẩm", Resolve(page, overrideHeader, "vi"));
        Assert.Equal("Product List", Resolve(page, overrideHeader, "en"));
    }

    [Fact]
    public void ResolveTitle_AfterBackNavigation_IgnoresCustomerDetailHeader()
    {
        var productListPage = CreatePage("product-list", "Danh sách", "List");
        var customerDetailHeader = new PageHeader("customer-detail", HeaderKind.Default, "CustomerDetail.headerTitle");

        var title = Resolve(productListPage, customerDetailHeader, "vi");

        Assert.Equal("Danh sách", title);
    }

    private static ActivePage CreatePage(string id, string vietnameseTitle, string englishTitle)
    {
        var menuItem = new MenuItem
        {
            Id = id,
            Titles = new Dictionary<string, string>
            {
                ["vi"] = vietnameseTitle,
                ["en"] = englishTitle
            },
            Assembly = "Test.dll",
            Component = "Test.Component"
        };

        return new ActivePage(id, menuItem, typeof(string));
    }

    private static string Resolve(ActivePage page, PageHeader? header, string language) =>
        PageHeaderResolver.ResolveTitle(
            page,
            header,
            menuItem => menuItem.Titles[language],
            (key, fallback) => (key, language) switch
            {
                ("ProductList.headerTitle", "vi") => "Danh sách sản phẩm",
                ("ProductList.headerTitle", "en") => "Product List",
                _ => fallback
            });
}
