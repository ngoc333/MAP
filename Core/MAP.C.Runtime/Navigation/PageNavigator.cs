using MAP.C.Contract.Diagnostics;
using MAP.C.Contract.Menus;
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

    public ActivePage? Current { get; private set; }
    public bool CanBack => _history.Count > 0;
    public event Action? Changed;

    public PageNavigator(IMenuService menuService, IModuleLoader moduleLoader, ILogger<PageNavigator> logger)
    {
        _menuService = menuService;
        _moduleLoader = moduleLoader;
        _logger = logger;
    }

    public async Task OpenAsync(string pageId, object? parameters = null, bool pushHistory = true)
    {
        if (Current?.PageId == pageId && parameters is null)
        {
            _logger.LogDebug("Skipping page {PageId}; already current with no new parameters", pageId);
            return;
        }

        try
        {
            var next = await CreateActivePageAsync(pageId, parameters);
            var isReplacingCurrent = Current?.PageId == pageId;

            if (Current is not null && pushHistory && !isReplacingCurrent)
                _history.Push(new PageHistoryEntry(Current.PageId, Current.Parameters));

            Current = next;
            SafeInvokeChanged(pageId);

            _logger.LogInformation(
                "Page opened. PageId={PageId} PushHistory={PushHistory} HistoryDepth={HistoryDepth}",
                pageId,
                pushHistory,
                _history.Count);
        }
        catch (Exception ex)
        {
            var errorId = ModuleErrorId.GetOrCreate(ex);
            _logger.LogError(ex, "Navigation failed. ErrorId={ErrorId} PageId={PageId}", errorId, pageId);
            throw;
        }
    }

    public async Task OpenRootAsync(string pageId, object? parameters = null)
    {
        try
        {
            var next = await CreateActivePageAsync(pageId, parameters);

            _history.Clear();
            Current = next;
            SafeInvokeChanged(pageId);

            _logger.LogInformation("Root page opened. PageId={PageId}", pageId);
        }
        catch (Exception ex)
        {
            var errorId = ModuleErrorId.GetOrCreate(ex);
            _logger.LogError(ex, "Root navigation failed. ErrorId={ErrorId} PageId={PageId}", errorId, pageId);
            throw;
        }
    }

    public async Task BackAsync()
    {
        if (_history.Count == 0)
        {
            _logger.LogWarning("BackAsync called but cannot go back. HistoryDepth={HistoryDepth}", _history.Count);
            return;
        }

        var previous = _history.Peek();

        try
        {
            var next = await CreateActivePageAsync(previous.PageId, previous.Parameters);

            _history.Pop();
            Current = next;
            SafeInvokeChanged(next.PageId);

            _logger.LogInformation(
                "Navigated back. PageId={PageId} HistoryDepth={HistoryDepth}",
                next.PageId,
                _history.Count);
        }
        catch (Exception ex)
        {
            var errorId = ModuleErrorId.GetOrCreate(ex);
            _logger.LogError(ex, "Back navigation failed. ErrorId={ErrorId} PageId={PageId}", errorId, previous.PageId);
            throw;
        }
    }

    private async Task<ActivePage> CreateActivePageAsync(string pageId, object? parameters)
    {
        var pageParameters = PageParams.From(parameters, out var parameterException);
        if (parameterException is not null)
            _logger.LogError(parameterException, "Failed to convert parameters for page {PageId}", pageId);

        var menuItem = _menuService.FindById(pageId)
            ?? throw new InvalidOperationException($"Page not found: {pageId}");
        var componentType = await _moduleLoader.LoadComponentAsync(menuItem);

        return new ActivePage(pageId, menuItem, componentType, pageParameters);
    }

    private void SafeInvokeChanged(string pageId)
    {
        if (Changed is null)
            return;

        foreach (var subscriber in Changed.GetInvocationList())
        {
            try
            {
                ((Action)subscriber)();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Navigation Changed subscriber failed. PageId={PageId} Subscriber={Subscriber}",
                    pageId, subscriber.Method.Name);
            }
        }
    }
}
