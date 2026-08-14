using MAP.C.Contract.Menus;
using MAP.C.Contract.Models;
using MAP.C.Contract.Modules;
using MAP.C.Runtime.Navigation;
using Microsoft.Extensions.Logging.Abstractions;

namespace MAP.C.Runtime.Tests;

public sealed class PageNavigatorTests
{
    private sealed class FakeMenuService : IMenuService
    {
        private readonly Dictionary<string, MenuItem> _menus = new();
        public List<MenuItem> Menus => _menus.Values.ToList();
        public event Action? OnMenusLoaded;
        public void Register(string pageId) => _menus[pageId] = new MenuItem { Id = pageId, Component = "TestComponent", Assembly = "Test.dll" };
        public MenuItem? FindById(string id) => _menus.GetValueOrDefault(id);
        public Task LoadMenusAsync() { OnMenusLoaded?.Invoke(); return Task.CompletedTask; }
    }

    private sealed class FakeModuleLoader : IModuleLoader
    {
        private Func<MenuItem, Task<Type>>? _loadFunc;
        public event Action<bool>? OnLoadingChanged;
        public void SetLoadFunc(Func<MenuItem, Task<Type>>? loadFunc) => _loadFunc = loadFunc;
        public async Task<Type> LoadComponentAsync(MenuItem menuItem)
        {
            OnLoadingChanged?.Invoke(true);
            try { return _loadFunc is null ? typeof(string) : await _loadFunc(menuItem); }
            finally { OnLoadingChanged?.Invoke(false); }
        }
    }

    private static PageNavigator CreateNavigator(FakeMenuService menus, FakeModuleLoader loader) =>
        new(menus, loader, NullLogger<PageNavigator>.Instance);

    [Fact]
    public async Task OpenAsync_DefaultPushHistory_BackReturnsToPreviousPage()
    {
        var (menus, loader, navigator) = CreateNavigator("A", "B");
        await navigator.OpenAsync("A");
        await navigator.OpenAsync("B");
        await navigator.BackAsync();

        Assert.Equal("A", navigator.Current!.PageId);
        Assert.False(navigator.CanBack);
    }

    [Fact]
    public async Task OpenAsync_WithoutHistoryPush_SkipsCurrentPageOnBack()
    {
        var (menus, loader, navigator) = CreateNavigator("A", "B", "C");
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
        var (menus, loader, navigator) = CreateNavigator("A", "B", "C", "D");
        await navigator.OpenAsync("A");
        await navigator.OpenAsync("B");
        await navigator.OpenAsync("C");
        await navigator.OpenAsync("D", pushHistory: false);

        await navigator.BackAsync();
        Assert.Equal("B", navigator.Current!.PageId);
        await navigator.BackAsync();
        Assert.Equal("A", navigator.Current!.PageId);
    }

    [Fact]
    public async Task OpenRootAsync_ClearsHistoryAndRecreatesSamePage()
    {
        var (menus, loader, navigator) = CreateNavigator("A", "B", "D");
        await navigator.OpenAsync("A");
        await navigator.OpenAsync("B");
        await navigator.OpenRootAsync("D");
        var firstD = navigator.Current;
        await navigator.OpenRootAsync("D");

        Assert.NotSame(firstD, navigator.Current);
        Assert.False(navigator.CanBack);
    }

    [Fact]
    public async Task OpenAsync_SamePageWithoutParameters_DoesNotRecreateOrNavigate()
    {
        var (menus, loader, navigator) = CreateNavigator("A");
        await navigator.OpenAsync("A");
        var first = navigator.Current;
        var navigatingCount = 0;
        navigator.Navigating += () => navigatingCount++;

        await navigator.OpenAsync("A");

        Assert.Same(first, navigator.Current);
        Assert.Equal(0, navigatingCount);
    }

    [Fact]
    public async Task OpenAsync_SamePageWithParameters_ReplacesCurrentWithoutAddingHistory()
    {
        var (menus, loader, navigator) = CreateNavigator("A", "Detail");
        await navigator.OpenAsync("A");
        await navigator.OpenAsync("Detail", new { Id = 1 });
        var firstDetail = navigator.Current;
        await navigator.OpenAsync("Detail", new { Id = 2 });

        Assert.NotSame(firstDetail, navigator.Current);
        Assert.Equal(2, navigator.Current!.Parameters!["Id"]);
        await navigator.BackAsync();
        Assert.Equal("A", navigator.Current!.PageId);
    }

    [Fact]
    public async Task Navigation_CancelsCurrentBeforeSlowTargetLoads()
    {
        var (menus, loader, navigator) = CreateNavigator("A", "B");
        await navigator.OpenAsync("A");
        var cancellationObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        navigator.Navigating += cancellationObserved.SetResult;
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<Type>(TaskCreationOptions.RunContinuationsAsynchronously);
        loader.SetLoadFunc(menu => menu.Id == "B" ? LoadWhenReleased(started, release) : Task.FromResult<Type>(typeof(string)));

        var openingB = navigator.OpenAsync("B");
        await started.Task;

        Assert.True(cancellationObserved.Task.IsCompleted);
        Assert.Equal("A", navigator.Current!.PageId);
        release.SetResult(typeof(string));
        await openingB;
        Assert.Equal("B", navigator.Current!.PageId);
    }

    [Fact]
    public async Task NavigationDuringSlowLoad_IsIgnored()
    {
        var (menus, loader, navigator) = CreateNavigator("A", "B", "C");
        await navigator.OpenAsync("A");
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<Type>(TaskCreationOptions.RunContinuationsAsynchronously);
        loader.SetLoadFunc(menu => menu.Id == "B" ? LoadWhenReleased(started, release) : Task.FromResult<Type>(typeof(string)));

        var openingB = navigator.OpenAsync("B");
        await started.Task;
        await navigator.OpenAsync("C");
        release.SetResult(typeof(string));
        await openingB;

        Assert.Equal("B", navigator.Current!.PageId);
    }

    [Fact]
    public async Task LoadFailure_RecreatesCancelledCurrentWithoutChangingHistory()
    {
        var (menus, loader, navigator) = CreateNavigator("A", "B", "D");
        await navigator.OpenAsync("A");
        await navigator.OpenAsync("B");
        var before = navigator.Current;
        loader.SetLoadFunc(menu => menu.Id == "D"
            ? throw new InvalidOperationException("Module load failed")
            : Task.FromResult<Type>(typeof(string)));

        await Assert.ThrowsAsync<InvalidOperationException>(() => navigator.OpenAsync("D"));

        Assert.NotSame(before, navigator.Current);
        Assert.Equal("B", navigator.Current!.PageId);
        Assert.True(navigator.CanBack);
        await navigator.BackAsync();
        Assert.Equal("A", navigator.Current!.PageId);
    }

    private static (FakeMenuService Menus, FakeModuleLoader Loader, PageNavigator Navigator) CreateNavigator(params string[] pageIds)
    {
        var menus = new FakeMenuService();
        foreach (var pageId in pageIds) menus.Register(pageId);
        var loader = new FakeModuleLoader();
        return (menus, loader, CreateNavigator(menus, loader));
    }

    private static async Task<Type> LoadWhenReleased(TaskCompletionSource started, TaskCompletionSource<Type> release)
    {
        started.SetResult();
        return await release.Task;
    }
}
