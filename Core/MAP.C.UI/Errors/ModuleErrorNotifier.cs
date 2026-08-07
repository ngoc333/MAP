using MAP.C.Contract.Config;
using MAP.C.Contract.Localization;
using MAP.C.Contract.Models;
using Radzen;

namespace MAP.C.UI.Errors;

/// <summary>
/// Centralized module error notification service.
/// Shows Radzen Notification for module errors based on configuration.
/// Does NOT log exceptions — logging is owned by PageNavigator or ModuleErrorBoundary.
/// </summary>
public sealed class ModuleErrorNotifier
{
    private const int NotificationDuration = 6000; // 6 seconds
    private const string DefaultFallbackMessage = "Chức năng tạm thời không khả dụng.";

    private readonly NotificationService _notificationService;
    private readonly IAppConfigService _configService;
    private readonly ILanguageService _languageService;

    public ModuleErrorNotifier(
        NotificationService notificationService,
        IAppConfigService configService,
        ILanguageService languageService)
    {
        _notificationService = notificationService;
        _configService = configService;
        _languageService = languageService;
    }

    /// <summary>
    /// Shows a notification for a module error if configured to do so.
    /// </summary>
    /// <param name="errorId">Short error ID for user reference (e.g., "A83F28C1").</param>
    public void Notify(string errorId)
    {
        var config = _configService.Current;
        if (config is null || !config.ShowModuleErrorNotification)
            return;

        var message = ResolveMessage(config);
        var summary = _languageService.T("moduleError.title", "Lỗi chức năng");
        var fullMessage = $"{summary}\n\n{message}\nMã lỗi: {errorId}";

        _notificationService.Notify(new NotificationMessage
        {
            Severity = NotificationSeverity.Error,
            Summary = summary,
            Detail = fullMessage,
            Duration = NotificationDuration
        });
    }

    private string ResolveMessage(AppConfig config)
    {
        if (!string.IsNullOrWhiteSpace(config.ModuleErrorMessage))
            return config.ModuleErrorMessage;

        // Try localized fallback
        var localized = _languageService.T("moduleError.defaultMessage", "");
        if (!string.IsNullOrWhiteSpace(localized))
            return localized;

        return DefaultFallbackMessage;
    }
}
