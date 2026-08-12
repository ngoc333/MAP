namespace MAP.C.Contract.Models;

public class MenuItem
{
    public string Id { get; set; } = string.Empty;

    public Dictionary<string, string> Titles { get; set; } = [];

    public string? Icon { get; set; }

    public string? Assembly { get; set; }

    public string? Component { get; set; }

    public List<MenuItem> Children { get; set; } = [];

    public bool HasChildren => Children.Count > 0;

    public bool IsPage =>
        !string.IsNullOrWhiteSpace(Assembly) &&
        !string.IsNullOrWhiteSpace(Component);
}

public class PageConfig
{
    public string Source { get; set; } = "local";
    public string? DbName { get; set; }
    public string? DbFunction { get; set; }
    public string? StartPageId { get; set; }
    public MenuItem? StartPage { get; set; }
    public List<MenuItem> Menus { get; set; } = new();
}
