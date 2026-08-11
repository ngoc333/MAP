using MAP.C.Contract.Config;
using MAP.C.Contract.Localization;
using MAP.C.Contract.Navigation;
using MAP.C.Contract.Diagnostics;
using MAP.C.UI.Errors;
using Microsoft.AspNetCore.Components;
using MAP.C.UI.Headers;

namespace MAP.C.UI.Pages;

public abstract class BasePage : ComponentBase, IDisposable
{
    [Inject]
    protected IPageNavigator Navigator { get; private set; } = default!;

    [Inject]
    protected IPageHeaderState Header { get; private set; } = default!;

    [Inject]
    protected ILanguageService Lang { get; private set; } = default!;

    [Inject]
    protected IUiStateService UiState { get; private set; } = default!;

    [Inject]
    protected ModuleErrorNotifier ErrorNotifier { get; private set; } = default!;

    protected object? PageParameters => Navigator.Current?.RawParameters;

    protected string? FromPageId => Navigator.Current?.FromPageId;

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

    private void OnLanguageChanged()
    {
        InvokeAsync(StateHasChanged);
    }

    protected bool IsMenuVisible => UiState.ShowMenu;
    protected bool IsHeaderVisible => UiState.ShowHeader;

    protected void SetMenuVisible(bool visible) => UiState.SetMenu(visible);
    protected void SetHeaderVisible(bool visible) => UiState.SetHeader(visible);
    protected void ToggleMenu() => UiState.ToggleMenu();
    protected void ToggleHeader() => UiState.ToggleHeader();

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
