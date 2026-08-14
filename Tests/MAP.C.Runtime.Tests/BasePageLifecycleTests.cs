using MAP.C.Contract.Localization;
using MAP.C.UI.Pages;

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
        public CancellationToken Token => PageCancellationToken;

        public void Initialize() => base.OnInitialized();

        protected override void DisposePage() => SyncDisposeCount++;

        protected override ValueTask DisposePageAsync()
        {
            AsyncDisposeCount++;
            return ValueTask.CompletedTask;
        }
    }

    [Fact]
    public async Task DisposePipeline_CancelsToken_UnsubscribesAndRunsHooksOnlyOnce()
    {
        var languageService = new FakeLanguageService();
        var page = new TestPage();
        typeof(BasePage)
            .GetProperty("Lang", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .SetValue(page, languageService);
        page.Initialize();

        Assert.False(page.Token.IsCancellationRequested);
        Assert.Equal(1, languageService.SubscriptionCount);

        await ((IAsyncDisposable)page).DisposeAsync();
        ((IDisposable)page).Dispose();

        Assert.True(page.Token.IsCancellationRequested);
        Assert.Equal(0, languageService.SubscriptionCount);
        Assert.Equal(1, page.SyncDisposeCount);
        Assert.Equal(1, page.AsyncDisposeCount);
    }
}
