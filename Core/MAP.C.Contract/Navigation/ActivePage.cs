using MAP.C.Contract.Models;

namespace MAP.C.Contract.Navigation;

public sealed record ActivePage(
    string PageId,
    MenuItem MenuItem,
    Type ComponentType,
    PageParams? Parameters = null)
{
    /// <summary>Gets the unique render identity assigned to this page navigation instance.</summary>
    public long InstanceId { get; init; }
}
