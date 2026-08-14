namespace MAP.C.Contract.Navigation;

/// <summary>Represents a page that can be restored from the back-navigation history.</summary>
public sealed record PageHistoryEntry(
    string PageId,
    PageParams? Parameters);
