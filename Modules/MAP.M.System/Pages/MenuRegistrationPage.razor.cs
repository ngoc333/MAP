using MAP.C.Contract.Context;
using Microsoft.AspNetCore.Components;
using Radzen;

namespace MAP.M.System.Pages;

public partial class MenuRegistrationPage
{
    [Inject]
    protected IClientContextService ClientContext { get; private set; } = default!;

    [Inject]
    protected DialogService Dialogs { get; private set; } = default!;

    [Inject]
    protected NotificationService Notifications { get; private set; } = default!;

    private string? UserName => ClientContext.Current.UserName;

    private string? IpAddress => ClientContext.Current.IpAddress;

    private void NotifySuccess(string message) => Notify(message, NotificationSeverity.Success);

    private void NotifyWarning(string message) => Notify(message, NotificationSeverity.Warning);

    private void NotifyError(string message) => Notify(message, NotificationSeverity.Error);

    private void Notify(string message, NotificationSeverity severity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        Notifications.Notify(new NotificationMessage
        {
            Severity = severity,
            Detail = message
        });
    }
}
