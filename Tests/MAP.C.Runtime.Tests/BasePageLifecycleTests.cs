using MAP.C.Contract.Localization;
using MAP.C.Contract.Models;
using MAP.C.Contract.Navigation;
using MAP.C.UI.Headers;
using MAP.C.UI.Pages;
using Microsoft.Extensions.Logging.Abstractions;

namespace MAP.C.Runtime.Tests;

public sealed class BasePageLifecycleTests
{
    private sealed class FakeLanguageService : ILanguageService
    {
        private Action? _languageChanged;

        public int SubscriptionCount { get; private set; }
        public string CurrentLanguage => "en";
        public IReadOnlyList<LanguageInfo> AvailableLanguages => [];

        public event Action? LanguageChanged
        {
            add { _languageChanged += value; SubscriptionCount++; }
            remove { _languageChanged -= value; SubscriptionCount--; }
        }

        public string T(string key) => key;
        public string T(string key, string defaultValue) => defaultValue;
        public void SetLanguage(string language) => _languageChanged?.Invoke();
        public Task LoadModuleResourcesAsync(string moduleName, Dictionary<string, Dictionary<string, object>> vi, Dictionary<string, Dictionary<string, object>> en) => Task.CompletedTask;
    }

    private sealed class TestPage : BasePage
    {
        public int AsyncDisposeCount { get; private set; }
        public bool ThrowOnAsyncDispose { get; init; }
        public CancellationToken Token => PageCancellationToken;

        public void Initialize() => base.OnInitialized();

        protected override ValueTask DisposePageAsync()
        {
            AsyncDisposeCount++;
            return ThrowOnAsyncDispose
                ? ValueTask.FromException(new InvalidOperationException("Async cleanup failed."))
                : ValueTask.CompletedTask;
        }
    }

    [Fact]
    public async Task Navigating_CancelsPageTokenImmediately()
    {
        var navigator = new FakePageNavigator();
        var page = new TestPage();
        ConfigurePage(page, new FakeLanguageService(), navigator);
        page.Initialize();

        navigator.RaiseNavigating();

        Assert.True(page.Token.IsCancellationRequested);
        await page.DisposeAsync();
    }

    [Fact]
    public async Task DisposePipeline_CancelsToken_UnsubscribesAndRunsHookOnlyOnce()
    {
        var languageService = new FakeLanguageService();
        var navigator = new FakePageNavigator();
        var page = new TestPage();
        ConfigurePage(page, languageService, navigator);
        page.Initialize();

        Assert.False(page.Token.IsCancellationRequested);
        Assert.Equal(1, languageService.SubscriptionCount);
        Assert.Equal(1, navigator.NavigatingSubscriptionCount);

        await page.DisposeAsync();
        await page.DisposeAsync();

        Assert.True(page.Token.IsCancellationRequested);
        Assert.Equal(0, languageService.SubscriptionCount);
        Assert.Equal(0, navigator.NavigatingSubscriptionCount);
        Assert.Equal(1, page.AsyncDisposeCount);
    }

    [Fact]
    public async Task DisposePipeline_IsolatesCleanupAndCancellationCallbackFailures()
    {
        var navigator = new FakePageNavigator();
        var page = new TestPage { ThrowOnAsyncDispose = true };
        ConfigurePage(page, new FakeLanguageService(), navigator);
        page.Initialize();
        page.Token.Register(() => throw new InvalidOperationException("Cancellation callback failed."));

        var exception = await Record.ExceptionAsync(async () => await page.DisposeAsync());

        Assert.Null(exception);
        Assert.True(page.Token.IsCancellationRequested);
        Assert.Equal(1, page.AsyncDisposeCount);
    }

    private static void ConfigurePage(TestPage page, FakeLanguageService languageService, FakePageNavigator navigator)
    {
        SetInjectedProperty(page, "Lang", languageService);
        SetInjectedProperty(page, "Header", new PageHeaderState());
        SetInjectedProperty(page, "Navigator", navigator);
        SetInjectedProperty(page, "Logger", NullLogger<BasePage>.Instance);
    }

    private static void SetInjectedProperty(object target, string name, object value) =>
        typeof(BasePage).GetProperty(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .SetValue(target, value);

    private sealed class FakePageNavigator : IPageNavigator
    {
        private Action? _navigating;

        public int NavigatingSubscriptionCount { get; private set; }
        public ActivePage? Current { get; } = new("A", new MenuItem { Id = "A" }, typeof(TestPage));
        public bool CanBack => false;
        public event Action? Navigating
        {
            add { _navigating += value; NavigatingSubscriptionCount++; }
            remove { _navigating -= value; NavigatingSubscriptionCount--; }
        }
        public event Action? Changed { add { } remove { } }
        public Task OpenAsync(string pageId, object? parameters = null, bool pushHistory = true) => Task.CompletedTask;
        public Task OpenRootAsync(string pageId, object? parameters = null) => Task.CompletedTask;
        public Task BackAsync() => Task.CompletedTask;
        public void RaiseNavigating() => _navigating?.Invoke();
    }
}
