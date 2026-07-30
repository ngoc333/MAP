using MAP.C.Contract.Localization;
using MAP.C.Contract.Navigation;
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

    protected object? PageParameters => Navigator.Current?.RawParameters;

    protected string? FromPageId => Navigator.Current?.FromPageId;

    protected virtual string HeaderTitleKey => string.Empty;

    protected virtual HeaderKind HeaderKind => HeaderKind.Default;

    protected virtual RenderFragment? HeaderStart => null;

    protected virtual RenderFragment? HeaderCenter => null;

    protected virtual RenderFragment? HeaderEnd => null;

    protected string HeaderTitle =>
        string.IsNullOrEmpty(HeaderTitleKey) ? string.Empty : Lang.T(HeaderTitleKey);

    protected void RefreshHeader()
    {
        Header.Set(new PageHeader(HeaderKind, HeaderTitle, HeaderStart, HeaderCenter, HeaderEnd));
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
        RefreshHeader();
        InvokeAsync(StateHasChanged);
    }

    public void Dispose()
    {
        Lang.LanguageChanged -= OnLanguageChanged;
    }
}
