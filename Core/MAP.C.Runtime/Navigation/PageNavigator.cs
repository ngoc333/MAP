using System.Text.Json;
using Microsoft.Extensions.Logging;
using MAP.C.Contract.Navigation;
using MAP.C.Contract.Menus;
using MAP.C.Contract.Modules;
using MAP.C.Contract.Models;
using MAP.C.Contract.UI.Pages;

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
        _logger.LogInformation("Mở page {PageId}, params: {Params}", pageId, paramPreview);

        var menuItem = _menuService.FindById(pageId)
            ?? throw new InvalidOperationException($"Không tìm thấy page với ID: {pageId}");

        var type = await _moduleLoader.LoadComponentAsync(menuItem)
            ?? throw new InvalidOperationException($"Không thể load component: {menuItem.Component}");

        var fromPageId = _stack.Count > 0 ? _stack.Peek().PageId : null;
        _stack.Push(new ActivePage(pageId, menuItem, type, pageParameters, fromPageId));
        Changed?.Invoke();

        _logger.LogInformation("Đã mở page {PageId} ({Type})", pageId, type.FullName);
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
