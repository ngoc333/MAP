using MAP.C.Contract.Diagnostics;
using MAP.C.Contract.Navigation;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;

namespace MAP.C.UI.Errors;

/// <summary>
/// Custom ErrorBoundary for module render/lifecycle errors.
/// Uses OnErrorAsync (proper hook) instead of side-effects in ErrorContent.
/// Logs full exception with module context and notifies via ModuleErrorNotifier.
/// </summary>
public sealed class ModuleErrorBoundary : ErrorBoundary
{
    [Inject]
    private ILogger<ModuleErrorBoundary> Logger { get; set; } = default!;

    [Inject]
    private ModuleErrorNotifier ErrorNotifier { get; set; } = default!;

    [Inject]
    private IPageNavigator Navigator { get; set; } = default!;

    /// <summary>
    /// Callback invoked when a module error is caught.
    /// Used by PageContainer/MainLayout to track faulted page state.
    /// </summary>
    [Parameter]
    public EventCallback OnFaulted { get; set; }

    protected override async Task OnErrorAsync(Exception exception)
    {
        var errorId = ModuleErrorId.GetOrCreate(exception);

        var active = Navigator.Current;
        Logger.LogError(exception,
            "Module render/lifecycle failed. ErrorId={ErrorId} PageId={PageId} Assembly={Assembly} Component={Component}",
            errorId,
            active?.PageId,
            active?.MenuItem.Assembly,
            active?.ComponentType.FullName);

        ErrorNotifier.Notify(errorId);

        if (OnFaulted.HasDelegate)
            await OnFaulted.InvokeAsync();
    }
}
