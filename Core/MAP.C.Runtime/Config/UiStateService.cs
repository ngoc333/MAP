using MAP.C.Contract.Config;

namespace MAP.C.Runtime.Config;

public class UiStateService : IUiStateService
{
    private bool _showMenu = true;
    private bool _showHeader = true;

    public bool ShowMenu => _showMenu;
    public bool ShowHeader => _showHeader;

    public event Action? Changed;

    public void ToggleMenu()
    {
        _showMenu = !_showMenu;
        Changed?.Invoke();
    }

    public void ToggleHeader()
    {
        _showHeader = !_showHeader;
        Changed?.Invoke();
    }

    public void SetMenu(bool visible)
    {
        if (_showMenu == visible) return;
        _showMenu = visible;
        Changed?.Invoke();
    }

    public void SetHeader(bool visible)
    {
        if (_showHeader == visible) return;
        _showHeader = visible;
        Changed?.Invoke();
    }
}
