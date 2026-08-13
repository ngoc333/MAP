using MAP.C.Contract.Context;
using MAP.C.Contract.Database;
using MAP.C.Contract.Diagnostics;
using MAP.C.Contract.Localization;
using MAP.C.Contract.Menus;
using MAP.C.Contract.Navigation;
using MAP.C.UI.Errors;
using MAP.C.UI.Headers;
using Microsoft.AspNetCore.Components;
using Radzen;
using System.Globalization;
using System.Text.Json;

namespace MAP.C.UI.Pages;

/// <summary>
/// Base component for MAP module pages. Inherit from this type to access navigation,
/// localization, notifications, confirmation dialogs, and the configured database API.
/// </summary>
public abstract class BasePage : ComponentBase, IDisposable
{
    [Inject]
    protected IPageNavigator Navigator { get; private set; } = default!;

    [Inject]
    protected IPageHeaderState Header { get; private set; } = default!;

    [Inject]
    protected ILanguageService Lang { get; private set; } = default!;

    [Inject]
    protected IDbApiClient DbClient { get; private set; } = default!;

    [Inject]
    protected IClientContextService ClientContext { get; private set; } = default!;

    [Inject]
    protected IMenuService MenuService { get; private set; } = default!;

    [Inject]
    protected DialogService Dialogs { get; private set; } = default!;

    [Inject]
    protected NotificationService Notifications { get; private set; } = default!;

    [Inject]
    protected ModuleErrorNotifier ErrorNotifier { get; private set; } = default!;

    /// <summary>Gets the raw parameters supplied when the current page was opened.</summary>
    protected object? PageParameters => Navigator.Current?.RawParameters;

    /// <summary>Gets the page identifier that opened the current page.</summary>
    protected string? FromPageId => Navigator.Current?.FromPageId;

    /// <summary>Gets the configured database name for the current menu.</summary>

    protected string DbName =>
        MenuService.DbName
        ?? throw new InvalidOperationException("Database name is not configured.");

    /// <summary>Gets the user name supplied by the current client context.</summary>
    protected string? UserName => ClientContext.Current.UserName;

    /// <summary>Gets the client IP address supplied by the current client context.</summary>
    protected string? IpAddress => ClientContext.Current.IpAddress;

    /// <summary>
    /// Gets an optional navigation parameter converted to <typeparamref name="T"/>.
    /// Returns <see langword="default"/> when the parameter is absent or cannot be converted.
    /// </summary>
    protected T? GetParameter<T>(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return TryGetParameter(name, out T? value) ? value : default;
    }

    /// <summary>
    /// Gets a required navigation parameter converted to <typeparamref name="T"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the parameter is absent or cannot be converted.</exception>
    protected T RequireParameter<T>(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (TryGetParameter(name, out T? value))
            return value!;

        throw new InvalidOperationException(
            $"Required page parameter '{name}' is missing or cannot be converted to {typeof(T).Name}.");
    }

    /// <summary>Shows a success notification.</summary>
    protected void NotifySuccess(string message) => Notify(message, NotificationSeverity.Success);

    /// <summary>Shows a warning notification.</summary>
    protected void NotifyWarning(string message) => Notify(message, NotificationSeverity.Warning);

    /// <summary>Shows an error notification.</summary>
    protected void NotifyError(string message) => Notify(message, NotificationSeverity.Error);

    /// <summary>Shows a confirmation dialog and returns <see langword="true"/> only when confirmed.</summary>
    protected async Task<bool> ConfirmAsync(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        return await Dialogs.Confirm(message) == true;
    }

    /// <summary>Queries a PostgreSQL function using the current menu database.</summary>
    protected Task<List<T>> QueryAsync<T>(
        string commandName,
        object? parameters = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandName);
        return DbClient.QueryPostgreSqlFunctionAsync<T>(
            DbName, commandName, parameters ?? new { }, cancellationToken);
    }

    /// <summary>Executes a PostgreSQL procedure using the current menu database.</summary>
    protected Task ExecuteAsync(
        string commandName,
        object? parameters = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandName);
        return DbClient.ExecutePostgreSqlProcedureAsync(
            DbName, commandName, parameters ?? new { }, cancellationToken);
    }

    protected virtual string? HeaderTitleKey => null;

    protected virtual HeaderKind HeaderKind => HeaderKind.Default;

    protected virtual RenderFragment? HeaderContent => null;

    protected virtual bool ShowBack => true;

    protected void RefreshHeader()
    {
        Header.Set(new PageHeader(
            Navigator.Current?.PageId ?? string.Empty,
            HeaderKind,
            HeaderTitleKey,
            HeaderContent,
            ShowBack));
    }

    protected override void OnInitialized()
    {
        Lang.LanguageChanged += OnLanguageChanged;
    }

    protected override void OnAfterRender(bool firstRender)
    {
        if (firstRender)
            RefreshHeader();
    }

    private bool TryGetParameter<T>(string name, out T? value)
    {
        value = default;
        var pageParams = PageParams.From(PageParameters, out _);
        if (pageParams is null || pageParams[name] is not { } rawValue)
            return false;

        if (rawValue is T typedValue)
        {
            value = typedValue;
            return true;
        }

        try
        {
            if (rawValue is JsonElement jsonElement)
            {
                value = jsonElement.Deserialize<T>(DbJson.Options);
                return value is not null || default(T) is null;
            }

            if (rawValue is IConvertible)
            {
                var targetType = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
                value = (T?)Convert.ChangeType(rawValue, targetType, CultureInfo.InvariantCulture);
                return true;
            }

            value = JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(rawValue, DbJson.Options), DbJson.Options);
            return value is not null || default(T) is null;
        }
        catch (Exception)
        {
            value = default;
            return false;
        }
    }

    private void Notify(string message, NotificationSeverity severity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        Notifications.Notify(new NotificationMessage
        {
            Severity = severity,
            Detail = message
        });
    }

    private void OnLanguageChanged()
    {
        InvokeAsync(StateHasChanged);
    }

    /// <summary>
    /// Safe navigation method for Module pages.
    /// Catches navigation exceptions and shows notification instead of
    /// letting the error propagate to the Module's ErrorBoundary.
    /// </summary>
    protected async Task OpenPageAsync(string pageId, object? parameters = null)
    {
        try
        {
            await Navigator.OpenAsync(pageId, parameters);
        }
        catch (Exception ex)
        {
            // Logging is owned by PageNavigator — don't duplicate here
            ErrorNotifier.Notify(ModuleErrorId.GetOrCreate(ex));
        }
    }

    public void Dispose()
    {
        Lang.LanguageChanged -= OnLanguageChanged;
    }
}
