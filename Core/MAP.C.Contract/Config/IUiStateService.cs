namespace MAP.C.Contract.Config;

public interface IUiStateService
{
    bool ShowMenu { get; }
    bool ShowHeader { get; }
    event Action? Changed;
    void ToggleMenu();
    void ToggleHeader();
    void SetMenu(bool visible);
    void SetHeader(bool visible);
}
