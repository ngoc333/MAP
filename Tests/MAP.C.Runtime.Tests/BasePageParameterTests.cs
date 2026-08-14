using System.Reflection;
using System.Text.Json;
using MAP.C.Contract.Models;
using MAP.C.Contract.Navigation;
using MAP.C.UI.Pages;

namespace MAP.C.Runtime.Tests;

public sealed class BasePageParameterTests
{
    [Fact]
    public void GetParameter_ConvertsAnonymousObjectValues()
    {
        var page = CreatePage(new { Count = 42, Code = "KH001", Day = "Friday" });

        Assert.Equal(42, page.GetOptional<int>("Count"));
        Assert.Equal("KH001", page.GetOptional<string>("Code"));
        Assert.Equal(DayOfWeek.Friday, page.GetOptional<DayOfWeek>("Day"));
    }

    [Fact]
    public void GetParameter_ConvertsJsonElementAndReturnsDefaultForMissingOrInvalidValues()
    {
        using var document = JsonDocument.Parse("42");
        var page = CreatePage(new { Number = document.RootElement.Clone(), Invalid = "not-an-int" });

        Assert.Equal(42, page.GetOptional<int?>("Number"));
        Assert.Null(page.GetOptional<int?>("Missing"));
        Assert.Equal(0, page.GetOptional<int>("Invalid"));
    }

    [Fact]
    public void RequireParameter_ThrowsForMissingOrInvalidValues()
    {
        var page = CreatePage(new { Invalid = "not-an-int" });

        Assert.Throws<InvalidOperationException>(() => page.GetRequired<int>("Missing"));
        Assert.Throws<InvalidOperationException>(() => page.GetRequired<int>("Invalid"));
    }

    private static TestPage CreatePage(object parameters)
    {
        var page = new TestPage();
        var navigator = new FakePageNavigator
        {
            Current = new ActivePage(
                "test",
                new MenuItem { Id = "test" },
                typeof(TestPage),
                PageParams.From(parameters, out _))
        };

        typeof(BasePage)
            .GetProperty("Navigator", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(page, navigator);
        return page;
    }

    private sealed class TestPage : BasePage
    {
        public T? GetOptional<T>(string name) => GetParameter<T>(name);

        public T GetRequired<T>(string name) => RequireParameter<T>(name);
    }

    private sealed class FakePageNavigator : IPageNavigator
    {
        public ActivePage? Current { get; set; }

        public bool CanBack => false;

        public event Action? Changed
        {
            add { }
            remove { }
        }

        public Task OpenAsync(string pageId, object? parameters = null, bool pushHistory = true) => Task.CompletedTask;

        public Task OpenRootAsync(string pageId, object? parameters = null) => Task.CompletedTask;

        public Task BackAsync() => Task.CompletedTask;
    }
}
