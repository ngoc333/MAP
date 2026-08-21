using MAP.C.Contract.Database;
using MAP.C.Contract.Diagnostics;
using MAP.C.Contract.Localization;
using MAP.C.Contract.Menus;
using MAP.C.Contract.Navigation;
using MAP.C.UI.Errors;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Text.Json;

namespace MAP.C.UI.Pages;

/// <summary>
/// Base component for MAP module pages. Provides the capabilities shared by nearly every page:
/// navigation, localization, database access, navigation parameters, and page lifecycle handling.
/// Inject optional UI or platform services directly in pages that need them.
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

    /// <summary>Gets localized text from core and loaded module resources.</summary>
    [Inject]
    protected ILanguageService Lang { get; private set; } = default!;

    /// <summary>Gets the low-level client for configured database API calls.</summary>
    [Inject]
    protected IDbApiClient DbClient { get; private set; } = default!;

    /// <summary>Gets the loaded menu and its current database configuration.</summary>
    [Inject]
    protected IMenuService MenuService { get; private set; } = default!;

    /// <summary>Gets the notifier used to show safely correlated module navigation errors.</summary>
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

            value = JsonSerializer.Deserialize<T>(
                JsonSerializer.Serialize(rawValue, DbJson.Options),
                DbJson.Options);
            return value is not null || default(T) is null;
        }
        catch (Exception)
        {
            value = default;
            return false;
        }
    }

    /// <summary>Queries a PostgreSQL function and returns the validated raw API response.</summary>
    protected Task<JsonElement> QueryAsync(
        string commandName,
        object? parameters = null,
        CancellationToken? cancellationToken = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandName);
        return DbClient.QueryPostgreSqlFunctionAsync(
            DbName,
            commandName,
            parameters ?? new { },
            cancellationToken ?? PageCancellationToken);
    }

    /// <summary>Queries a PostgreSQL function and maps its array data to a list.</summary>
    protected Task<List<T>> QueryAsync<T>(
        string commandName,
        object? parameters = null,
        CancellationToken? cancellationToken = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandName);
        return DbClient.QueryPostgreSqlFunctionAsync<T>(
            DbName,
            commandName,
            parameters ?? new { },
            cancellationToken ?? PageCancellationToken);
    }

    /// <summary>Queries a PostgreSQL function and maps its data to a single model.</summary>
    protected Task<T?> QuerySingleAsync<T>(
        string commandName,
        object? parameters = null,
        CancellationToken? cancellationToken = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandName);
        return DbClient.QuerySinglePostgreSqlFunctionAsync<T>(
            DbName,
            commandName,
            parameters ?? new { },
            cancellationToken ?? PageCancellationToken);
    }

    /// <summary>Executes a PostgreSQL procedure and returns the validated raw API response.</summary>
    protected Task<JsonElement> ExecuteAsync(
        string commandName,
        object? parameters = null,
        CancellationToken? cancellationToken = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandName);
        return DbClient.ExecutePostgreSqlProcedureAsync(
            DbName,
            commandName,
            parameters ?? new { },
            cancellationToken ?? PageCancellationToken);
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
    /// Opens another module page and converts navigation failures to the standard module error notification.
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
            // Logging is owned by PageNavigator — don't duplicate here.
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
