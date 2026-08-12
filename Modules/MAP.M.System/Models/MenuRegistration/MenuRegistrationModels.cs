namespace MAP.M.System.Models.MenuRegistration;

public sealed class PageRegistration
{
    public string PageId { get; set; } = "";
    public string TitleVi { get; set; } = "";
    public string? TitleEn { get; set; }
    public string? Icon { get; set; }
    public string AssemblyName { get; set; } = "";
    public string ComponentName { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public string? Note { get; set; }
    public DateTimeOffset? UpdDate { get; set; }
}

public sealed class ProgramRegistration
{
    public string ProgramId { get; set; } = "";
    public string? StartPageId { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Note { get; set; }
}

public sealed class MenuRegistrationRow
{
    public string ProgramId { get; set; } = "";
    public string MenuId { get; set; } = "";
    public string? ParentMenuId { get; set; }
    public string? PageId { get; set; }
    public string? TitleVi { get; set; }
    public string? TitleEn { get; set; }
    public string? Icon { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Note { get; set; }
    public bool IsGroup => string.IsNullOrWhiteSpace(PageId);
}
