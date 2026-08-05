using MAP.C.Contract.Config;

namespace MAP.C.Wpf.Config;

public sealed class PlatformCapabilities : IPlatformCapabilities
{
    public bool SupportsFullscreen => true;
    public bool SupportsHideTaskbar => true;
    public bool SupportsDisplaySelection => true;
}
