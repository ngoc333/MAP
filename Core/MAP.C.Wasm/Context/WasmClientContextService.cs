using MAP.C.Contract.Context;
using MAP.C.Contract.Config;

namespace MAP.C.Wasm.Context;

public sealed class WasmClientContextService(IAppConfigService configService) : IClientContextService
{
    public ClientContext Current => new(configService.Current?.ProgramId ?? "MAP", null, null, null);
}
