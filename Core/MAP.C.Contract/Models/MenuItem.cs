namespace MAP.C.Contract.Models;

public class MenuItem
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string? Assembly { get; set; }
    public string? Component { get; set; }
    public List<MenuItem>? Children { get; set; }

    public bool HasChildren => Children is { Count: > 0 };
    public bool IsPage => !string.IsNullOrEmpty(Assembly) && !string.IsNullOrEmpty(Component);
}

public class PageConfig
{
    public string Source { get; set; } = "local";
    public string? DbName { get; set; }
    public string? DbFunction { get; set; }
    public List<MenuItem> Menus { get; set; } = new();
}
