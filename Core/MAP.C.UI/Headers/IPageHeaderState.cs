namespace MAP.C.UI.Headers;

public interface IPageHeaderState
{
    PageHeader? Active { get; }
    event Action? Changed;
    void Set(PageHeader header);
}
