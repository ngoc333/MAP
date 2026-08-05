namespace MAP.C.Contract.Config;

public interface IPlatformCapabilities
{
    bool SupportsFullscreen { get; }
    bool SupportsHideTaskbar { get; }
    bool SupportsDisplaySelection { get; }
}
