using MAP.C.Contract.Models;

namespace MAP.C.Contract.Navigation;

public sealed record ActivePage(
    string PageId,
    MenuItem MenuItem,
    Type ComponentType,
    object? RawParameters = null,
    string? FromPageId = null);
