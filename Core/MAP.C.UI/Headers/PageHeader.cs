using Microsoft.AspNetCore.Components;

namespace MAP.C.UI.Headers;

public sealed record PageHeader(
    HeaderKind Kind,
    string Title,
    RenderFragment? Content = null,
    bool ShowBack = true);
