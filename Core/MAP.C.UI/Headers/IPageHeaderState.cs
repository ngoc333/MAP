namespace MAP.C.UI.Headers;

public interface IPageHeaderState
{
    PageHeader? Active { get; }
    event Action? Changed;
    void Set(PageHeader header);

    /// <summary>
    /// Clears the active header only when it belongs to <paramref name="pageId"/>.
    /// </summary>
    void Clear(string pageId);
}
