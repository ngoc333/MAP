using Microsoft.AspNetCore.Components;

namespace MAP.C.UI.Headers;

public sealed record PageHeader(
    HeaderKind Kind,
    string Title,
    RenderFragment? Start = null,
    RenderFragment? Center = null,
    RenderFragment? End = null);
