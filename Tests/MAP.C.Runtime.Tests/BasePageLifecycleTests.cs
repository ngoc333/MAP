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
            add
            {
                _languageChanged += value;
                SubscriptionCount++;
            }
            remove
            {
                _languageChanged -= value;
                SubscriptionCount--;
            }
        }

        public string T(string key) => key;
        public string T(string key, string defaultValue) => defaultValue;
        public void SetLanguage(string language) => _languageChanged?.Invoke();
        public Task LoadModuleResourcesAsync(string moduleName, Dictionary<string, Dictionary<string, object>> vi, Dictionary<string, Dictionary<string, object>> en) => Task.CompletedTask;
    }

    private sealed class TestPage : BasePage
    {
        public int SyncDisposeCount { get; private set; }
        public int AsyncDisposeCount { get; private set; }
        public bool ThrowOnSyncDispose { get; init; }
        public bool ThrowOnAsyncDispose { get; init; }
        public CancellationToken Token => PageCancellationToken;

        public void Initialize() => base.OnInitialized();

        protected override void DisposePage()
        {
            SyncDisposeCount++;
            if (ThrowOnSyncDispose)
                throw new InvalidOperationException("Sync cleanup failed.");
        }

        protected override ValueTask DisposePageAsync()
        {
            AsyncDisposeCount++;
            return ThrowOnAsyncDispose
                ? ValueTask.FromException(new InvalidOperationException("Async cleanup failed."))
                : ValueTask.CompletedTask;
        }
    }

    [Fact]
    public async Task DisposePipeline_CancelsToken_UnsubscribesAndRunsHooksOnlyOnce()
    {
        var languageService = new FakeLanguageService();
        var page = new TestPage();
        ConfigurePage(page, languageService, new PageHeaderState());
        page.Initialize();

        Assert.False(page.Token.IsCancellationRequested);
        Assert.Equal(1, languageService.SubscriptionCount);

        await page.DisposeAsync();
        await page.DisposeAsync();

        Assert.True(page.Token.IsCancellationRequested);
        Assert.Equal(0, languageService.SubscriptionCount);
        Assert.Equal(1, page.SyncDisposeCount);
        Assert.Equal(1, page.AsyncDisposeCount);
    }

    [Fact]
    public async Task DisposePipeline_DoesNotClearHeaderOwnedByNewerPage()
    {
        var languageService = new FakeLanguageService();
        var headerState = new PageHeaderState();
        var page = new TestPage();
        ConfigurePage(page, languageService, headerState);
        page.Initialize();
        var newerHeader = new PageHeader("B", HeaderKind.Default, "title");
        headerState.Set(newerHeader);

        await page.DisposeAsync();

        Assert.Same(newerHeader, headerState.Active);
    }

    [Fact]
    public async Task DisposePipeline_IsolatesCleanupAndCancellationCallbackFailures()
    {
        var languageService = new FakeLanguageService();
        var headerState = new PageHeaderState();
        var page = new TestPage { ThrowOnSyncDispose = true, ThrowOnAsyncDispose = true };
        ConfigurePage(page, languageService, headerState);
        page.Initialize();
        headerState.Set(new PageHeader("A", HeaderKind.Default, "title"));
        page.Token.Register(() => throw new InvalidOperationException("Cancellation callback failed."));

        var exception = await Record.ExceptionAsync(async () => await page.DisposeAsync());

        Assert.Null(exception);
        Assert.True(page.Token.IsCancellationRequested);
        Assert.Equal(0, languageService.SubscriptionCount);
        Assert.Null(headerState.Active);
        Assert.Equal(1, page.SyncDisposeCount);
        Assert.Equal(1, page.AsyncDisposeCount);
    }

    private static void ConfigurePage(TestPage page, FakeLanguageService languageService, IPageHeaderState headerState)
    {
        SetInjectedProperty(page, "Lang", languageService);
        SetInjectedProperty(page, "Header", headerState);
        SetInjectedProperty(page, "Navigator", new FakePageNavigator());
        SetInjectedProperty(page, "Logger", NullLogger<BasePage>.Instance);
    }

    private static void SetInjectedProperty(object target, string name, object value) =>
        typeof(BasePage)
            .GetProperty(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .SetValue(target, value);

    private sealed class FakePageNavigator : IPageNavigator
    {
        public ActivePage? Current { get; } = new("A", new MenuItem { Id = "A" }, typeof(TestPage));
        public bool CanBack => false;
        public event Action? Changed { add { } remove { } }
        public Task OpenAsync(string pageId, object? parameters = null, bool pushHistory = true) => Task.CompletedTask;
        public Task OpenRootAsync(string pageId, object? parameters = null) => Task.CompletedTask;
        public Task BackAsync() => Task.CompletedTask;
    }
}
