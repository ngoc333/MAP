namespace MAP.C.Contract.Models;

public class AppConfig
{
    public bool Fullscreen { get; set; } = false;
    public bool HideTaskbar { get; set; } = false;
    public int DisplayIndex { get; set; } = 0;
    public bool ShowMenu { get; set; } = true;
    public string AppTitle { get; set; } = "MAP";
    public string DefaultLanguage { get; set; } = "vi";
    public string DefaultPageId { get; set; } = "home-dashboard";
    public string MenuSource { get; set; } = "db";
    public bool ShowModuleErrorNotification { get; set; } = true;
    public string? ModuleErrorMessage { get; set; }
}
