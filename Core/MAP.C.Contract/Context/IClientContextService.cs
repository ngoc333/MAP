namespace MAP.C.Contract.Context;

/// <summary>Provides the host-supplied context for the active MAP client session.</summary>
public interface IClientContextService
{
    /// <summary>Gets the immutable context for the active client session.</summary>
    ClientContext Current { get; }
}
