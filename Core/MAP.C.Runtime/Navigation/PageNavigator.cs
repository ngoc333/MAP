using System.Diagnostics;
using Microsoft.Extensions.Logging;
using MAP.C.Contract.Navigation;
using MAP.C.Contract.Menus;
using MAP.C.Contract.Modules;
using MAP.C.Contract.Models;
using MAP.C.Contract.Logging;

namespace MAP.C.Runtime.Navigation;

public sealed class PageNavigator : IPageNavigator
{
    private readonly IMenuService _menuService;
    private readonly IModuleLoader _moduleLoader;
    private readonly ILogger<PageNavigator> _logger;
    private readonly Stack<ActivePage> _stack = new();

    public ActivePage? Current => _stack.Count > 0 ? _stack.Peek() : null;
    public bool CanBack => _stack.Count > 1;
    public event Action? Changed;

    public PageNavigator(IMenuService menuService, IModuleLoader moduleLoader, ILogger<PageNavigator> logger)
    {
        _menuService = menuService;
        _moduleLoader = moduleLoader;
        _logger = logger;
    }

    public async Task OpenAsync(string pageId, object? parameters = null)
    {
        var navigationId = Guid.NewGuid().ToString("N");
        var started = Stopwatch.GetTimestamp();
        var fromPageId = _stack.Count > 0 ? _stack.Peek().PageId : null;
        using var operation = DiagnosticContext.BeginOperation(navigationId);
        _logger.LogInformation("Navigation started. NavigationId={NavigationId} PageId={PageId} FromPageId={FromPageId}", navigationId, pageId, fromPageId);

        try
        {
            // Check if already on same page with no new parameters — skip silently
            if (_stack.Count > 0 && _stack.Peek().PageId == pageId && parameters is null)
            {
                _logger.LogInformation("Skipping page {PageId} — already current, no new parameters", pageId);
                return;
            }

            // Determine if this is a replace-same-page operation
            // Preserve FromPageId from the old page when replacing
            bool isReplace = _stack.Count > 0 && _stack.Peek().PageId == pageId;
            if (isReplace)
            {
                // Keep the old page's FromPageId so navigation history is preserved
                fromPageId = _stack.Peek().FromPageId;
                _logger.LogInformation("Re-opening page {PageId} with new parameters, preserving FromPageId={FromPageId}", pageId, fromPageId);
            }

            var pageParameters = PageParams.From(parameters, out var parameterException);
            if (parameterException is not null)
                _logger.LogError(parameterException, "Failed to convert parameters for page {PageId}", pageId);

            var paramPreview = CreateParameterPreview(parameters);
            _logger.LogInformation("Opening page. NavigationId={NavigationId} PageId={PageId} Params={Params}", navigationId, pageId, paramPreview);

            // Prepare everything before modifying the stack
            var menuItem = _menuService.FindById(pageId)
                ?? throw new InvalidOperationException($"Page not found: {pageId}");

            var type = await _moduleLoader.LoadComponentAsync(menuItem);

            // All preparation succeeded — now safely modify the stack
            if (isReplace)
            {
                _stack.Pop();
            }

            _stack.Push(new ActivePage(pageId, menuItem, type, pageParameters, fromPageId));

            // Notify UI subscribers — subscriber errors must not break navigation
            SafeInvokeChanged(navigationId, pageId);

            _logger.LogInformation("Navigation completed. NavigationId={NavigationId} PageId={PageId} Component={Component} StackDepth={StackDepth} DurationMs={DurationMs}",
                navigationId, pageId, type.FullName, _stack.Count, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Navigation failed. NavigationId={NavigationId} PageId={PageId} FromPageId={FromPageId} DurationMs={DurationMs}",
                navigationId, pageId, fromPageId, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            throw;
        }
    }

    public Task BackAsync()
    {
        if (!CanBack)
        {
            _logger.LogWarning("BackAsync called but cannot go back. StackDepth={StackDepth}", _stack.Count);
            return Task.CompletedTask;
        }

        var fromPage = _stack.Pop();
        var toPage = _stack.Peek();

        _logger.LogInformation("Navigated back. FromPageId={FromPageId} ToPageId={ToPageId} StackDepth={StackDepth}",
            fromPage.PageId, toPage.PageId, _stack.Count);

        SafeInvokeChanged("back", toPage.PageId);
        return Task.CompletedTask;
    }

    private void SafeInvokeChanged(string navigationId, string pageId)
    {
        if (Changed is null) return;

        foreach (var subscriber in Changed.GetInvocationList())
        {
            try
            {
                ((Action)subscriber)();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Navigation Changed subscriber failed. NavigationId={NavigationId} PageId={PageId} Subscriber={Subscriber}",
                    navigationId, pageId, subscriber.Method.Name);
                // Do not rollback stack, do not re-throw — this is a UI notification failure only
            }
        }
    }

    private static string CreateParameterPreview(object? parameters)
    {
        if (parameters is null) return "null";

        try
        {
            // Only log type name and property names — never log values to avoid leaking sensitive data
            var type = parameters.GetType();
            var properties = type.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                .Select(p => p.Name)
                .ToArray();

            if (properties.Length == 0)
                return $"Type={type.Name} Properties=[]";

            return $"Type={type.Name} Properties=[{string.Join(",", properties)}]";
        }
        catch
        {
            return parameters.GetType().FullName ?? "unknown";
        }
    }
}
