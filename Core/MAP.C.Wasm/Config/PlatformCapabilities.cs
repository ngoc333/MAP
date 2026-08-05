using MAP.C.Contract.Config;

namespace MAP.C.Wasm.Config;

public sealed class PlatformCapabilities : IPlatformCapabilities
{
    public bool SupportsFullscreen => false;
    public bool SupportsHideTaskbar => false;
    public bool SupportsDisplaySelection => false;
}
