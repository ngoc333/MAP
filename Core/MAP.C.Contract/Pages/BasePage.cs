using MAP.C.Contract.Navigation;
using MAP.C.Contract.Shell;
using Microsoft.AspNetCore.Components;

namespace MAP.C.Contract.Pages;

public abstract class BasePage : ComponentBase
{
    [Inject]
    protected IPageNavigator Navigator { get; private set; } = default!;

    [Inject]
    protected IPageHeaderState Header { get; private set; } = default!;

    protected object? PageParameters => Navigator.Current?.RawParameters;

    protected string? FromPageId => Navigator.Current?.FromPageId;

    protected virtual string HeaderTitle => string.Empty;

    protected virtual HeaderKind HeaderKind => HeaderKind.Default;

    protected virtual RenderFragment? HeaderStart => null;

    protected virtual RenderFragment? HeaderCenter => null;

    protected virtual RenderFragment? HeaderEnd => null;

    protected void RefreshHeader()
    {
        Header.Set(new PageHeader(HeaderKind, HeaderTitle, HeaderStart, HeaderCenter, HeaderEnd));
    }

    protected override void OnAfterRender(bool firstRender)
    {
        if (firstRender)
            RefreshHeader();
    }
}
