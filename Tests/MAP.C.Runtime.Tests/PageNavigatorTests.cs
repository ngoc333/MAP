using MAP.C.Contract.Diagnostics;
using MAP.C.Contract.Models;
using MAP.C.Contract.Menus;
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

        public void Register(string pageId, string component = "TestComponent", string assembly = "Test.dll")
        {
            _menus[pageId] = new MenuItem { Id = pageId, Component = component, Assembly = assembly };
        }

        public MenuItem? FindById(string id) =>
            _menus.TryGetValue(id, out var item) ? item : null;

        public Task LoadMenusAsync() => Task.CompletedTask;
    }

    private sealed class FakeModuleLoader : IModuleLoader
    {
        private readonly Dictionary<string, Type> _cache = new();
        private Func<MenuItem, Task<Type>>? _loadFunc;

        public event Action<bool>? OnLoadingChanged;

        public void SetLoadFunc(Func<MenuItem, Task<Type>> func) => _loadFunc = func;

        public async Task<Type> LoadComponentAsync(MenuItem menuItem)
        {
            OnLoadingChanged?.Invoke(true);
            try
            {
                if (_loadFunc is not null)
                    return await _loadFunc(menuItem);
                return typeof(string);
            }
            finally
            {
                OnLoadingChanged?.Invoke(false);
            }
        }
    }

    private sealed class LogRecord
    {
        public LogLevel Level { get; init; }
        public EventId EventId { get; init; }
        public string? Message { get; init; }
        public Exception? Exception { get; init; }
        public Dictionary<string, object?> State { get; init; } = new();
    }

    private sealed class TestLogger<T> : ILogger<T>
    {
        public List<LogRecord> Records { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var record = new LogRecord
            {
                Level = logLevel,
                EventId = eventId,
                Message = formatter(state, exception),
                Exception = exception
            };

            // Capture structured state key-value pairs
            if (state is IEnumerable<KeyValuePair<string, object?>> pairs)
            {
                foreach (var pair in pairs)
                    record.State[pair.Key] = pair.Value;
            }

            Records.Add(record);
        }
    }

    private readonly FakeMenuService _menuService = new();
    private readonly FakeModuleLoader _moduleLoader = new();
    private readonly ILogger<PageNavigator> _logger = NullLogger<PageNavigator>.Instance;

    private PageNavigator CreateNavigator() =>
        new(_menuService, _moduleLoader, _logger);

    private (PageNavigator nav, TestLogger<PageNavigator> logger) CreateNavigatorWithLogger()
    {
        var logger = new TestLogger<PageNavigator>();
        var nav = new PageNavigator(_menuService, _moduleLoader, logger);
        return (nav, logger);
    }

    [Fact]
    public async Task OpenAsync_FirstPage_PushesPage()
    {
        var nav = CreateNavigator();
        _menuService.Register("home");

        await nav.OpenAsync("home");

        Assert.NotNull(nav.Current);
        Assert.Equal("home", nav.Current.PageId);
        Assert.False(nav.CanBack);
    }

    [Fact]
    public async Task OpenAsync_SamePageWithoutParameters_DoesNothing()
    {
        var nav = CreateNavigator();
        _menuService.Register("home");

        await nav.OpenAsync("home");
        var first = nav.Current;
        await nav.OpenAsync("home");

        Assert.Same(first, nav.Current);
        Assert.False(nav.CanBack);
    }

    [Fact]
    public async Task OpenAsync_SamePageWithParameters_ReplacesTopWithoutIncreasingDepth()
    {
        var nav = CreateNavigator();
        _menuService.Register("home");
        _menuService.Register("page1");

        await nav.OpenAsync("home");
        await nav.OpenAsync("page1");
        Assert.True(nav.CanBack);

        await nav.OpenAsync("page1", new { Id = 42 });

        Assert.Equal("page1", nav.Current!.PageId);
        Assert.True(nav.CanBack); // depth unchanged: still home -> page1
    }

    [Fact]
    public async Task OpenAsync_SamePageWithParameters_PreservesPreviousFromPageId()
    {
        var nav = CreateNavigator();
        _menuService.Register("home");
        _menuService.Register("page1");

        await nav.OpenAsync("home");
        await nav.OpenAsync("page1");
        await nav.OpenAsync("page1", new { Id = 42 });

        // When replacing same page, FromPageId should be preserved from the old page
        Assert.Equal("home", nav.Current!.FromPageId);
    }

    [Fact]
    public async Task OpenAsync_SamePageWhenLoaderFails_KeepsExistingPage()
    {
        var nav = CreateNavigator();
        _menuService.Register("home");
        _menuService.Register("page1");

        await nav.OpenAsync("home");
        await nav.OpenAsync("page1");
        var beforeReplace = nav.Current;

        // Now try to re-open page1 with params but make loader fail
        _moduleLoader.SetLoadFunc(mi =>
        {
            if (mi.Id == "page1")
                throw new InvalidOperationException("Module load failed");
            return Task.FromResult<Type>(typeof(string));
        });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            nav.OpenAsync("page1", new { Id = 42 }));

        // Stack should be unchanged
        Assert.Same(beforeReplace, nav.Current);
        Assert.Equal("page1", nav.Current!.PageId);
        Assert.True(nav.CanBack);
    }

    [Fact]
    public async Task OpenAsync_MenuNotFound_KeepsExistingStack()
    {
        var nav = CreateNavigator();
        _menuService.Register("home");

        await nav.OpenAsync("home");
        var before = nav.Current;

        // menu "missing" is not registered
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            nav.OpenAsync("missing"));

        Assert.Same(before, nav.Current);
        Assert.False(nav.CanBack);
    }

    [Fact]
    public async Task BackAsync_WithMultiplePages_ReturnsToPreviousPage()
    {
        var nav = CreateNavigator();
        _menuService.Register("home");
        _menuService.Register("page1");

        await nav.OpenAsync("home");
        await nav.OpenAsync("page1");
        Assert.True(nav.CanBack);

        await nav.BackAsync();

        Assert.Equal("home", nav.Current!.PageId);
        Assert.False(nav.CanBack);
    }

    [Fact]
    public async Task OpenAsync_SamePageWithParameters_BypassesSamePageCheck()
    {
        var nav = CreateNavigator();
        _menuService.Register("home");

        await nav.OpenAsync("home");
        var first = nav.Current;

        // Same page with parameters should create a new ActivePage instance
        await nav.OpenAsync("home", new { Refresh = true });

        Assert.NotNull(nav.Current);
        Assert.Equal("home", nav.Current.PageId);
        Assert.NotSame(first, nav.Current); // new ActivePage instance
    }

    [Fact]
    public async Task OpenAsync_Failure_AttachesErrorIdToException()
    {
        var nav = CreateNavigator();
        _menuService.Register("home");
        _menuService.Register("broken");

        _moduleLoader.SetLoadFunc(mi =>
        {
            if (mi.Id == "broken")
                throw new InvalidOperationException("Module load failed");
            return Task.FromResult<Type>(typeof(string));
        });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            nav.OpenAsync("broken"));

        // ErrorId should be attached to the exception
        Assert.NotNull(ex.Data["MAP.ErrorId"]);
        Assert.IsType<string>(ex.Data["MAP.ErrorId"]);
        Assert.Equal(8, ((string)ex.Data["MAP.ErrorId"]!).Length);
    }

    [Fact]
    public async Task OpenAsync_Failure_SameErrorIdInLogAndException()
    {
        // Verify ErrorId in exception matches ErrorId in structured log
        var (nav, logger) = CreateNavigatorWithLogger();
        _menuService.Register("broken");

        _moduleLoader.SetLoadFunc(mi =>
            throw new InvalidOperationException("Module load failed"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            nav.OpenAsync("broken"));

        var exceptionErrorId = ex.Data["MAP.ErrorId"] as string;
        Assert.NotNull(exceptionErrorId);

        // Find the "Navigation failed" log entry
        var failedLog = logger.Records
            .FirstOrDefault(r => r.Message != null && r.Message.Contains("Navigation failed"));
        Assert.NotNull(failedLog);

        // Verify structured ErrorId in log matches exception ErrorId
        Assert.True(failedLog.State.ContainsKey("ErrorId"), "Log state should contain ErrorId key");
        Assert.Equal(exceptionErrorId, failedLog.State["ErrorId"]?.ToString());
    }

    [Fact]
    public void ErrorId_IsExactly8UpperCaseHexChars()
    {
        var id = ModuleErrorId.Create();
        Assert.Equal(8, id.Length);
        Assert.Matches("^[0-9A-F]{8}$", id);
    }

    [Fact]
    public void ErrorId_SameExceptionRetainsSameId()
    {
        var ex = new InvalidOperationException("test");
        var id1 = ModuleErrorId.GetOrCreate(ex);
        var id2 = ModuleErrorId.GetOrCreate(ex);
        Assert.Equal(id1, id2);
    }

    [Fact]
    public void ErrorId_GetOrCreate_DoesNotReplaceExisting()
    {
        var ex = new InvalidOperationException("test");
        ModuleErrorId.Set(ex, "FIXED123");
        var result = ModuleErrorId.GetOrCreate(ex);
        Assert.Equal("FIXED123", result);
    }

    [Fact]
    public async Task ChangedSubscriberThrows_DoesNotFailNavigation()
    {
        var nav = CreateNavigator();
        _menuService.Register("home");

        var callCount = 0;
        nav.Changed += () =>
        {
            callCount++;
            if (callCount == 1) throw new InvalidOperationException("Subscriber error");
        };

        // Should not throw even though subscriber throws
        await nav.OpenAsync("home");

        Assert.Equal("home", nav.Current!.PageId);
    }
}
