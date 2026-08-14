namespace MAP.C.UI.Headers;

public interface IPageHeaderState
{
    PageHeader? Active { get; }
    event Action? Changed;
    void Set(PageHeader header);

    /// <summary>Clears the active page header.</summary>
    void Clear();
}
