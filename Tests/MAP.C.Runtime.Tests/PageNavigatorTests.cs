using MAP.C.Contract.Diagnostics;
using MAP.C.Contract.Menus;
using MAP.C.Contract.Models;
using MAP.C.Contract.Modules;
using MAP.C.Contract.Navigation;
using MAP.C.Runtime.Navigation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace MAP.C.Runtime.Tests;

public sealed class PageNavigatorTests
{
    private sealed class FakeMenuService : IMenuService
    {
        private readonly Dictionary<string, MenuItem> _menus = new();

        public List<MenuItem> Menus => _menus.Values.ToList();
        public event Action? OnMenusLoaded;

        public void Register(string pageId) =>
            _menus[pageId] = new MenuItem { Id = pageId, Component = "TestComponent", Assembly = "Test.dll" };

        public MenuItem? FindById(string id) => _menus.GetValueOrDefault(id);
        public Task LoadMenusAsync()
        {
            OnMenusLoaded?.Invoke();
            return Task.CompletedTask;
        }
    }

    private sealed class FakeModuleLoader : IModuleLoader
    {
        private Func<MenuItem, Task<Type>>? _loadFunc;

        public event Action<bool>? OnLoadingChanged;

        public void SetLoadFunc(Func<MenuItem, Task<Type>>? loadFunc) => _loadFunc = loadFunc;

        public async Task<Type> LoadComponentAsync(MenuItem menuItem)
        {
            OnLoadingChanged?.Invoke(true);
            try
            {
                return _loadFunc is null ? typeof(string) : await _loadFunc(menuItem);
            }
            finally
            {
                OnLoadingChanged?.Invoke(false);
            }
        }
    }

    private sealed class LogRecord
    {
        public string? Message { get; init; }
        public Dictionary<string, object?> State { get; init; } = new();
    }

    private sealed class TestLogger<T> : ILogger<T>
    {
        public List<LogRecord> Records { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var record = new LogRecord { Message = formatter(state, exception) };
            if (state is IEnumerable<KeyValuePair<string, object?>> pairs)
            {
                foreach (var pair in pairs)
                    record.State[pair.Key] = pair.Value;
            }

            Records.Add(record);
        }
    }

    private static PageNavigator CreateNavigator(FakeMenuService menus, FakeModuleLoader loader) =>
        new(menus, loader, NullLogger<PageNavigator>.Instance);

    [Fact]
    public async Task OpenAsync_FirstPage_SetsCurrentWithoutHistory()
    {
        var menus = new FakeMenuService();
        var loader = new FakeModuleLoader();
        menus.Register("A");

        var navigator = CreateNavigator(menus, loader);
        await navigator.OpenAsync("A");

        Assert.Equal("A", navigator.Current!.PageId);
        Assert.False(navigator.CanBack);
    }

    [Fact]
    public async Task OpenAsync_DefaultPushHistory_BackReturnsToPreviousPage()
    {
        var menus = new FakeMenuService();
        var loader = new FakeModuleLoader();
        menus.Register("A");
        menus.Register("B");
        var navigator = CreateNavigator(menus, loader);

        await navigator.OpenAsync("A");
        await navigator.OpenAsync("B");
        await navigator.BackAsync();

        Assert.Equal("A", navigator.Current!.PageId);
        Assert.False(navigator.CanBack);
    }

    [Fact]
    public async Task OpenAsync_WithoutHistoryPush_PreservesExistingBackTarget()
    {
        var menus = new FakeMenuService();
        var loader = new FakeModuleLoader();
        foreach (var pageId in new[] { "A", "B", "C" }) menus.Register(pageId);
        var navigator = CreateNavigator(menus, loader);

        await navigator.OpenAsync("A");
        await navigator.OpenAsync("B");
        await navigator.OpenAsync("C", pushHistory: false);
        await navigator.BackAsync();

        Assert.Equal("A", navigator.Current!.PageId);
        Assert.False(navigator.CanBack);
    }

    [Fact]
    public async Task OpenAsync_MultiLevelHistory_SkipsPageOpenedWithoutHistoryPush()
    {
        var menus = new FakeMenuService();
        var loader = new FakeModuleLoader();
        foreach (var pageId in new[] { "A", "B", "C", "D" }) menus.Register(pageId);
        var navigator = CreateNavigator(menus, loader);

        await navigator.OpenAsync("A");
        await navigator.OpenAsync("B");
        await navigator.OpenAsync("C");
        await navigator.OpenAsync("D", pushHistory: false);

        await navigator.BackAsync();
        Assert.Equal("B", navigator.Current!.PageId);
        await navigator.BackAsync();
        Assert.Equal("A", navigator.Current!.PageId);
        Assert.False(navigator.CanBack);
    }

    [Fact]
    public async Task OpenRootAsync_ClearsHistoryAfterTargetLoads()
    {
        var menus = new FakeMenuService();
        var loader = new FakeModuleLoader();
        foreach (var pageId in new[] { "A", "B", "C", "D" }) menus.Register(pageId);
        var navigator = CreateNavigator(menus, loader);

        await navigator.OpenAsync("A");
        await navigator.OpenAsync("B");
        await navigator.OpenAsync("C");
        await navigator.OpenRootAsync("D");

        Assert.Equal("D", navigator.Current!.PageId);
        Assert.False(navigator.CanBack);
    }

    [Fact]
    public async Task OpenRootAsync_LoadFailure_PreservesCurrentAndHistory()
    {
        var menus = new FakeMenuService();
        var loader = new FakeModuleLoader();
        foreach (var pageId in new[] { "A", "B", "D" }) menus.Register(pageId);
        var navigator = CreateNavigator(menus, loader);
        await navigator.OpenAsync("A");
        await navigator.OpenAsync("B");
        var before = navigator.Current;
        loader.SetLoadFunc(menu => menu.Id == "D"
            ? throw new InvalidOperationException("Module load failed")
            : Task.FromResult<Type>(typeof(string)));

        await Assert.ThrowsAsync<InvalidOperationException>(() => navigator.OpenRootAsync("D"));

        Assert.Same(before, navigator.Current);
        Assert.True(navigator.CanBack);
        await navigator.BackAsync();
        Assert.Equal("A", navigator.Current!.PageId);
    }

    [Fact]
    public async Task OpenAsync_LoadFailure_PreservesCurrentAndHistory()
    {
        var menus = new FakeMenuService();
        var loader = new FakeModuleLoader();
        foreach (var pageId in new[] { "A", "B", "D" }) menus.Register(pageId);
        var navigator = CreateNavigator(menus, loader);
        await navigator.OpenAsync("A");
        await navigator.OpenAsync("B");
        var before = navigator.Current;
        loader.SetLoadFunc(menu => menu.Id == "D"
            ? throw new InvalidOperationException("Module load failed")
            : Task.FromResult<Type>(typeof(string)));

        await Assert.ThrowsAsync<InvalidOperationException>(() => navigator.OpenAsync("D"));

        Assert.Same(before, navigator.Current);
        await navigator.BackAsync();
        Assert.Equal("A", navigator.Current!.PageId);
    }

    [Fact]
    public async Task BackAsync_LoadFailure_PreservesCurrentAndHistory()
    {
        var menus = new FakeMenuService();
        var loader = new FakeModuleLoader();
        foreach (var pageId in new[] { "A", "B" }) menus.Register(pageId);
        var navigator = CreateNavigator(menus, loader);
        await navigator.OpenAsync("A");
        await navigator.OpenAsync("B");
        var before = navigator.Current;
        loader.SetLoadFunc(menu => menu.Id == "A"
            ? throw new InvalidOperationException("Module load failed")
            : Task.FromResult<Type>(typeof(string)));

        await Assert.ThrowsAsync<InvalidOperationException>(() => navigator.BackAsync());

        Assert.Same(before, navigator.Current);
        Assert.True(navigator.CanBack);
    }

    [Fact]
    public async Task OpenAsync_SamePageWithoutParameters_DoesNotRecreateOrAddHistory()
    {
        var menus = new FakeMenuService();
        var loader = new FakeModuleLoader();
        menus.Register("A");
        var navigator = CreateNavigator(menus, loader);
        await navigator.OpenAsync("A");
        var first = navigator.Current;

        await navigator.OpenAsync("A");

        Assert.Same(first, navigator.Current);
        Assert.False(navigator.CanBack);
    }

    [Fact]
    public async Task OpenAsync_SamePageWithParameters_ReplacesCurrentWithoutAddingHistory()
    {
        var menus = new FakeMenuService();
        var loader = new FakeModuleLoader();
        foreach (var pageId in new[] { "A", "Detail" }) menus.Register(pageId);
        var navigator = CreateNavigator(menus, loader);
        await navigator.OpenAsync("A");
        await navigator.OpenAsync("Detail", new { Id = 1 });
        var firstDetail = navigator.Current;

        await navigator.OpenAsync("Detail", new { Id = 2 });

        Assert.NotSame(firstDetail, navigator.Current);
        Assert.Equal(2, navigator.Current!.Parameters! ["Id"]);
        await navigator.BackAsync();
        Assert.Equal("A", navigator.Current!.PageId);
        Assert.False(navigator.CanBack);
    }

    [Fact]
    public async Task OpenAsync_Failure_AttachesMatchingErrorIdToLogAndException()
    {
        var menus = new FakeMenuService();
        var loader = new FakeModuleLoader();
        menus.Register("broken");
        loader.SetLoadFunc(_ => throw new InvalidOperationException("Module load failed"));
        var logger = new TestLogger<PageNavigator>();
        var navigator = new PageNavigator(menus, loader, logger);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => navigator.OpenAsync("broken"));

        var errorId = Assert.IsType<string>(exception.Data["MAP.ErrorId"]);
        Assert.Matches("^[0-9A-F]{8}$", errorId);
        var failedLog = Assert.Single(logger.Records, record => record.Message!.Contains("Navigation failed"));
        Assert.Equal(errorId, failedLog.State["ErrorId"]?.ToString());
    }

    [Fact]
    public void ErrorId_IsExactly8UpperCaseHexChars()
    {
        Assert.Matches("^[0-9A-F]{8}$", ModuleErrorId.Create());
    }

    [Fact]
    public void ErrorId_SameExceptionRetainsSameId()
    {
        var exception = new InvalidOperationException("test");

        Assert.Equal(ModuleErrorId.GetOrCreate(exception), ModuleErrorId.GetOrCreate(exception));
    }

    [Fact]
    public void ErrorId_GetOrCreate_DoesNotReplaceExisting()
    {
        var exception = new InvalidOperationException("test");
        ModuleErrorId.Set(exception, "FIXED123");

        Assert.Equal("FIXED123", ModuleErrorId.GetOrCreate(exception));
    }

    [Fact]
    public async Task ChangedSubscriberThrows_DoesNotFailNavigation()
    {
        var menus = new FakeMenuService();
        var loader = new FakeModuleLoader();
        menus.Register("A");
        var navigator = CreateNavigator(menus, loader);
        navigator.Changed += () => throw new InvalidOperationException("Subscriber error");

        await navigator.OpenAsync("A");

        Assert.Equal("A", navigator.Current!.PageId);
    }
}
