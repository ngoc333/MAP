using MAP.C.Contract.Context;
using MAP.C.Contract.Database;
using MAP.C.Contract.Diagnostics;
using MAP.C.Contract.Localization;
using MAP.C.Contract.Menus;
using MAP.C.Contract.Navigation;
using MAP.C.UI.Errors;
using MAP.C.UI.Headers;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Radzen;
using System.Globalization;
using System.Text.Json;

namespace MAP.C.UI.Pages;

/// <summary>
/// Base component for MAP module pages. Inherit from this type to access navigation,
/// localization, notifications, confirmation dialogs, and the configured database API.
/// </summary>
public abstract class BasePage : ComponentBase, IAsyncDisposable
{
    private readonly CancellationTokenSource _pageCancellationTokenSource = new();
    private readonly CancellationToken _pageCancellationToken;
    private readonly object _disposeSyncRoot = new();
    private Task? _disposeTask;
    private string _pageId = string.Empty;
    private PageParams? _pageParameters;

    protected BasePage()
    {
        _pageCancellationToken = _pageCancellationTokenSource.Token;
    }

    /// <summary>Gets the service used to open pages and return to the previous page.</summary>
    [Inject]
    protected IPageNavigator Navigator { get; private set; } = default!;

    /// <summary>Gets the state used to render the host page header.</summary>
    [Inject]
    protected IPageHeaderState Header { get; private set; } = default!;

    /// <summary>Gets localized text from core and loaded module resources.</summary>
    [Inject]
    protected ILanguageService Lang { get; private set; } = default!;

    /// <summary>Gets the low-level client for configured database API calls.</summary>
    [Inject]
    protected IDbApiClient DbClient { get; private set; } = default!;

    /// <summary>Gets the current user, client IP address, and program context.</summary>
    [Inject]
    protected IClientContextService ClientContext { get; private set; } = default!;

    /// <summary>Gets the loaded menu and its current database configuration.</summary>
    [Inject]
    protected IMenuService MenuService { get; private set; } = default!;

    /// <summary>Gets the Radzen dialog service for advanced dialog scenarios.</summary>
    [Inject]
    protected DialogService Dialogs { get; private set; } = default!;

    /// <summary>Gets the Radzen notification service for advanced notification scenarios.</summary>
    [Inject]
    protected NotificationService Notifications { get; private set; } = default!;

    /// <summary>Gets the notifier used to show safely correlated module errors.</summary>
    [Inject]
    protected ModuleErrorNotifier ErrorNotifier { get; private set; } = default!;

    /// <summary>Gets the logger used to report isolated page cleanup failures.</summary>
    [Inject]
    protected ILogger<BasePage> Logger { get; private set; } = default!;

    /// <summary>Gets this page instance's navigation identifier captured during initialization.</summary>
    protected string PageId => _pageId;

    /// <summary>Gets the parameters captured for this page instance during initialization.</summary>
    protected PageParams? PageParameters => _pageParameters;

    /// <summary>Gets a token cancelled when this page instance leaves the UI.</summary>
    protected CancellationToken PageCancellationToken => _pageCancellationToken;

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
    /// Tries to get a navigation parameter converted to <typeparamref name="T"/>.
    /// </summary>
    protected bool TryGetParameter<T>(string name, out T? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        value = default;
        var pageParams = PageParameters;
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

            var targetType = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
            if (targetType.IsEnum)
            {
                value = rawValue is string enumName
                    ? (T)Enum.Parse(targetType, enumName, ignoreCase: true)
                    : (T)Enum.ToObject(targetType, rawValue);
                return true;
            }

            if (rawValue is IConvertible)
            {
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

    /// <summary>Queries a PostgreSQL function and returns the validated raw API response.</summary>
    protected Task<JsonElement> QueryAsync(
        string commandName,
        object? parameters = null,
        CancellationToken? cancellationToken = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandName);
        return DbClient.QueryPostgreSqlFunctionAsync(
            DbName, commandName, parameters ?? new { }, cancellationToken ?? PageCancellationToken);
    }

    /// <summary>Queries a PostgreSQL function and maps its array data to a list.</summary>
    protected Task<List<T>> QueryAsync<T>(
        string commandName,
        object? parameters = null,
        CancellationToken? cancellationToken = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandName);
        return DbClient.QueryPostgreSqlFunctionAsync<T>(
            DbName, commandName, parameters ?? new { }, cancellationToken ?? PageCancellationToken);
    }

    /// <summary>Queries a PostgreSQL function and maps its data to a single model.</summary>
    protected Task<T?> QuerySingleAsync<T>(
        string commandName,
        object? parameters = null,
        CancellationToken? cancellationToken = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandName);
        return DbClient.QuerySinglePostgreSqlFunctionAsync<T>(
            DbName, commandName, parameters ?? new { }, cancellationToken ?? PageCancellationToken);
    }

    /// <summary>Executes a PostgreSQL procedure and returns the validated raw API response.</summary>
    protected Task<JsonElement> ExecuteAsync(
        string commandName,
        object? parameters = null,
        CancellationToken? cancellationToken = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandName);
        return DbClient.ExecutePostgreSqlProcedureAsync(
            DbName, commandName, parameters ?? new { }, cancellationToken ?? PageCancellationToken);
    }

    /// <summary>Gets the localization key displayed as the page header title.</summary>
    /// <remarks>Override to set a localized title; return <see langword="null"/> when the page supplies its own header content.</remarks>
    protected virtual string? HeaderTitleKey => null;

    /// <summary>Gets the visual kind applied to the page header.</summary>
    protected virtual HeaderKind HeaderKind => HeaderKind.Default;

    /// <summary>Gets optional custom content rendered in the page header.</summary>
    protected virtual RenderFragment? HeaderContent => null;

    /// <summary>Gets whether the host should show a back button in the page header.</summary>
    protected virtual bool ShowBack => true;

    /// <summary>Refreshes the host header after page state or header properties change.</summary>
    protected void RefreshHeader()
    {
        Header.Set(new PageHeader(
            PageId,
            HeaderKind,
            HeaderTitleKey,
            HeaderContent,
            ShowBack));
    }

    /// <summary>
    /// Captures navigation state for this page instance. Derived overrides must call
    /// <c>base.OnInitialized()</c> before accessing navigation parameters.
    /// </summary>
    protected override void OnInitialized()
    {
        var current = Navigator.Current;
        _pageId = current?.PageId ?? string.Empty;
        _pageParameters = current?.Parameters;
        Navigator.Navigating += OnNavigating;
        Lang.LanguageChanged += OnLanguageChanged;
    }

    protected override void OnAfterRender(bool firstRender)
    {
        if (firstRender)
            RefreshHeader();
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

    private void OnNavigating()
    {
        try
        {
            _pageCancellationTokenSource.Cancel();
        }
        catch (Exception ex)
        {
            LogCleanupError(ex, "CancelOnNavigating");
        }
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
    protected async Task OpenPageAsync(
        string pageId,
        object? parameters = null,
        bool pushHistory = true)
    {
        try
        {
            await Navigator.OpenAsync(pageId, parameters, pushHistory);
        }
        catch (Exception ex)
        {
            // Logging is owned by PageNavigator — don't duplicate here
            ErrorNotifier.Notify(ModuleErrorId.GetOrCreate(ex));
        }
    }

    /// <summary>Releases page-specific resources.</summary>
    /// <remarks>Do not implement disposal interfaces in derived pages; use this hook instead. Failures are logged and isolated.</remarks>
    protected virtual ValueTask DisposePageAsync() => ValueTask.CompletedTask;

    /// <inheritdoc />
    public ValueTask DisposeAsync() => DisposeCoreAsync();

    private ValueTask DisposeCoreAsync()
    {
        lock (_disposeSyncRoot)
        {
            _disposeTask ??= DisposeCoreImplementationAsync();
            return new ValueTask(_disposeTask);
        }
    }

    private async Task DisposeCoreImplementationAsync()
    {
        SafeCleanup(() => Navigator.Navigating -= OnNavigating, "UnsubscribeNavigating");
        SafeCleanup(() => Lang.LanguageChanged -= OnLanguageChanged, "UnsubscribeLanguageChanged");
        SafeCleanup(_pageCancellationTokenSource.Cancel, "Cancel");

        try
        {
            await DisposePageAsync();
        }
        catch (Exception ex)
        {
            LogCleanupError(ex, "DisposePageAsync");
        }

        SafeCleanup(_pageCancellationTokenSource.Dispose, "DisposeCancellationTokenSource");
    }

    private void SafeCleanup(Action cleanup, string phase)
    {
        try
        {
            cleanup();
        }
        catch (Exception ex)
        {
            LogCleanupError(ex, phase);
        }
    }

    private void LogCleanupError(Exception exception, string phase)
    {
        Logger?.LogError(
            exception,
            "Page cleanup failed. PageId={PageId} Phase={Phase} Component={Component}",
            PageId,
            phase,
            GetType().FullName);
    }
}
