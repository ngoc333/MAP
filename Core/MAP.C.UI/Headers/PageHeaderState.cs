namespace MAP.C.UI.Headers;

public sealed class PageHeaderState : IPageHeaderState
{
    public PageHeader? Active { get; private set; }
    public event Action? Changed;

    public void Set(PageHeader header)
    {
        Active = header;
        Changed?.Invoke();
    }

    public void Clear(string pageId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pageId);

        if (Active?.PageId != pageId)
            return;

        Active = null;
        Changed?.Invoke();
    }
}
