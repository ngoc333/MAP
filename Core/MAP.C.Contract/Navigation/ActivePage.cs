using MAP.C.Contract.Models;

namespace MAP.C.Contract.Navigation;

/// <summary>Represents the page currently active in the application.</summary>
public sealed class ActivePage
{
    public ActivePage(
        string pageId,
        MenuItem menuItem,
        Type componentType,
        PageParams? parameters = null)
    {
        PageId = pageId;
        MenuItem = menuItem;
        ComponentType = componentType;
        Parameters = parameters;
    }

    public string PageId { get; }
    public MenuItem MenuItem { get; }
    public Type ComponentType { get; }
    public PageParams? Parameters { get; }
}
