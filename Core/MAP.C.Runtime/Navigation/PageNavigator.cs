using MAP.C.Contract.Diagnostics;
using MAP.C.Contract.Menus;
using MAP.C.Contract.Models;
using MAP.C.Contract.Modules;
using MAP.C.Contract.Navigation;
using Microsoft.Extensions.Logging;

namespace MAP.C.Runtime.Navigation;

public sealed class PageNavigator : IPageNavigator
{
    private readonly IMenuService _menuService;
    private readonly IModuleLoader _moduleLoader;
    private readonly ILogger<PageNavigator> _logger;
    private readonly Stack<PageHistoryEntry> _history = new();
    private bool _isNavigating;

    public ActivePage? Current { get; private set; }
    public bool CanBack => _history.Count > 0;
    public event Action? Navigating;
    public event Action? Changed;

    public PageNavigator(IMenuService menuService, IModuleLoader moduleLoader, ILogger<PageNavigator> logger)
    {
        _menuService = menuService;
        _moduleLoader = moduleLoader;
        _logger = logger;
    }

    public Task OpenAsync(string pageId, object? parameters = null, bool pushHistory = true) =>
        NavigateAsync(pageId, parameters, pushHistory, clearHistory: false, isBackNavigation: false);

    public Task OpenRootAsync(string pageId, object? parameters = null) =>
        NavigateAsync(pageId, parameters, pushHistory: false, clearHistory: true, isBackNavigation: false);

    public Task BackAsync()
    {
        if (_history.Count == 0)
        {
            _logger.LogWarning("BackAsync called but cannot go back. HistoryDepth={HistoryDepth}", _history.Count);
            return Task.CompletedTask;
        }

        var previous = _history.Peek();
        return NavigateAsync(previous.PageId, previous.Parameters, pushHistory: false, clearHistory: false, isBackNavigation: true);
    }

    private async Task NavigateAsync(
        string pageId,
        object? parameters,
        bool pushHistory,
        bool clearHistory,
        bool isBackNavigation)
    {
        if (_isNavigating)
        {
            _logger.LogDebug("Ignoring navigation request while another navigation is in progress. PageId={PageId}", pageId);
            return;
        }

        if (!clearHistory && !isBackNavigation && Current?.PageId == pageId && parameters is null)
        {
            _logger.LogDebug("Skipping page {PageId}; already current with no new parameters", pageId);
            return;
        }

        _isNavigating = true;
        var previousCurrent = Current;
        var hasLeftCurrent = false;
        try
        {
            var target = PrepareTarget(pageId, parameters);
            SafeInvokeNavigating(pageId);
            hasLeftCurrent = true;
            var next = await LoadTargetAsync(target);

            if (clearHistory)
                _history.Clear();
            else if (isBackNavigation)
                _history.Pop();
            else if (previousCurrent is not null && pushHistory && previousCurrent.PageId != pageId)
                _history.Push(new PageHistoryEntry(previousCurrent.PageId, previousCurrent.Parameters));

            Current = next;
            SafeInvokeChanged(next.PageId);
            _logger.LogInformation("Page opened. PageId={PageId} PushHistory={PushHistory} HistoryDepth={HistoryDepth}",
                next.PageId, pushHistory, _history.Count);
        }
        catch (Exception ex)
        {
            var errorId = ModuleErrorId.GetOrCreate(ex);
            _logger.LogError(ex, "Navigation failed. ErrorId={ErrorId} PageId={PageId}", errorId, pageId);

            if (hasLeftCurrent && previousCurrent is not null)
            {
                Current = new ActivePage(
                    previousCurrent.PageId,
                    previousCurrent.MenuItem,
                    previousCurrent.ComponentType,
                    previousCurrent.Parameters);
                SafeInvokeChanged(previousCurrent.PageId);
            }

            throw;
        }
        finally
        {
            _isNavigating = false;
        }
    }

    private NavigationTarget PrepareTarget(string pageId, object? parameters)
    {
        var pageParameters = PageParams.From(parameters, out var parameterException);
        if (parameterException is not null)
            _logger.LogError(parameterException, "Failed to convert parameters for page {PageId}", pageId);

        var menuItem = _menuService.FindById(pageId)
            ?? throw new InvalidOperationException($"Page not found: {pageId}");
        return new NavigationTarget(pageId, menuItem, pageParameters);
    }

    private async Task<ActivePage> LoadTargetAsync(NavigationTarget target)
    {
        var componentType = await _moduleLoader.LoadComponentAsync(target.MenuItem);
        return new ActivePage(target.PageId, target.MenuItem, componentType, target.Parameters);
    }

    private void SafeInvokeNavigating(string pageId) => SafeInvoke(Navigating, "Navigating", pageId);

    private void SafeInvokeChanged(string pageId) => SafeInvoke(Changed, "Changed", pageId);

    private void SafeInvoke(Action? callbacks, string eventName, string pageId)
    {
        if (callbacks is null)
            return;

        foreach (var subscriber in callbacks.GetInvocationList())
        {
            try
            {
                ((Action)subscriber)();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Navigation {EventName} subscriber failed. PageId={PageId} Subscriber={Subscriber}",
                    eventName, pageId, subscriber.Method.Name);
            }
        }
    }

    private sealed record NavigationTarget(string PageId, MenuItem MenuItem, PageParams? Parameters);
}
