using Microsoft.AspNetCore.Components;

namespace MAP.C.UI.Headers;

public sealed record PageHeader(
    string PageId,
    HeaderKind Kind,
    string? TitleKey = null,
    RenderFragment? Content = null,
    bool ShowBack = true);
