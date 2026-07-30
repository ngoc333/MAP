using System.Text.Json;
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
        if (_stack.Count > 0 && _stack.Peek().PageId == pageId)
        {
            _logger.LogInformation("Bỏ qua mở page {PageId} vì đang ở page hiện tại", pageId);
            return;
        }

        var pageParameters = PageParams.From(parameters, out var parameterException);
        if (parameterException is not null)
            _logger.LogError(parameterException, "Không thể chuyển tham số khi mở page {PageId}", pageId);

        var paramPreview = parameterException is not null || parameters is null
            ? "null"
            : Truncate(JsonSerializer.Serialize(parameters), 200);
        _logger.LogInformation("Opening page. NavigationId={NavigationId} PageId={PageId} Params={Params}", navigationId, pageId, paramPreview);

        var menuItem = _menuService.FindById(pageId)
            ?? throw new InvalidOperationException($"Không tìm thấy page với ID: {pageId}");

        var type = await _moduleLoader.LoadComponentAsync(menuItem)
            ?? throw new InvalidOperationException($"Không thể load component: {menuItem.Component}");

        _stack.Push(new ActivePage(pageId, menuItem, type, pageParameters, fromPageId));
        Changed?.Invoke();

        _logger.LogInformation("Navigation completed. NavigationId={NavigationId} PageId={PageId} Component={Component} StackDepth={StackDepth} DurationMs={DurationMs}", navigationId, pageId, type.FullName, _stack.Count, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Navigation failed. NavigationId={NavigationId} PageId={PageId} FromPageId={FromPageId} DurationMs={DurationMs}", navigationId, pageId, fromPageId, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            throw;
        }
    }

    public Task BackAsync()
    {
        if (CanBack)
        {
            var popped = _stack.Pop();
            Changed?.Invoke();
            _logger.LogInformation("Quay lại từ {PageId}", popped.PageId);
        }
        return Task.CompletedTask;
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength] + "...";
}
