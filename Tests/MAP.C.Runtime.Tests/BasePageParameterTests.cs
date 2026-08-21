using System.Reflection;
using System.Text.Json;
using MAP.C.Contract.Localization;
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
    public void TryGetParameter_ReturnsFalseForMissingOrInvalidValues()
    {
        var page = CreatePage(new { Count = 42, Invalid = "not-an-int" });

        Assert.True(page.TryGet<int>("Count", out var count));
        Assert.Equal(42, count);

        Assert.False(page.TryGet<int>("Missing", out _));
        Assert.False(page.TryGet<int>("Invalid", out _));
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
        typeof(BasePage)
            .GetProperty("Lang", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(page, new FakeLanguageService());
        page.Initialize();
        return page;
    }

    [Fact]
    public void InitializedPage_UsesItsNavigationSnapshotAfterCurrentChanges()
    {
        var page = new TestPage();
        var navigator = new FakePageNavigator
        {
            Current = CreateActivePage("A", 10)
        };

        SetInjectedProperty(page, "Navigator", navigator);
        SetInjectedProperty(page, "Lang", new FakeLanguageService());
        page.Initialize();
        navigator.Current = CreateActivePage("B", 20);

        Assert.Equal("A", page.CurrentPageId);
        Assert.Equal(10, page.GetOptional<int>("Id"));
    }

    private static ActivePage CreateActivePage(string pageId, int id) => new(
        pageId,
        new MenuItem { Id = pageId },
        typeof(TestPage),
        PageParams.From(new { Id = id }, out _));

    private static void SetInjectedProperty(object target, string name, object value) =>
        typeof(BasePage)
            .GetProperty(name, BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(target, value);

    private sealed class TestPage : BasePage
    {
        public string CurrentPageId => PageId;
        public T? GetOptional<T>(string name) => GetParameter<T>(name);
        public bool TryGet<T>(string name, out T? value) => TryGetParameter(name, out value);
        public void Initialize() => base.OnInitialized();
    }

    private sealed class FakeLanguageService : ILanguageService
    {
        public string CurrentLanguage => "en";
        public IReadOnlyList<LanguageInfo> AvailableLanguages => [];
        public event Action? LanguageChanged { add { } remove { } }
        public string T(string key) => key;
        public string T(string key, string defaultValue) => defaultValue;
        public void SetLanguage(string language) { }
        public Task LoadModuleResourcesAsync(string moduleName, Dictionary<string, Dictionary<string, object>> vi, Dictionary<string, Dictionary<string, object>> en) => Task.CompletedTask;
    }

    private sealed class FakePageNavigator : IPageNavigator
    {
        public ActivePage? Current { get; set; }

        public bool CanBack => false;

        public event Action? Navigating
        {
            add { }
            remove { }
        }

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
