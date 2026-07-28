using MAP.C.Contract.Shell;

namespace MAP.C.Components.Shell;

public sealed class PageHeaderState : IPageHeaderState
{
    public PageHeader? Active { get; private set; }
    public event Action? Changed;

    public void Set(PageHeader header)
    {
        Active = header;
        Changed?.Invoke();
    }
}
