namespace MAP.C.Contract.Context;

public interface IClientContextService
{
    ClientContext Current { get; }
}
