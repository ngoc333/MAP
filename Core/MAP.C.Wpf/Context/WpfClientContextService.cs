using System.Net;
using System.Net.Sockets;
using MAP.C.Contract.Context;
using MAP.C.Contract.Config;

namespace MAP.C.Wpf.Context;

public sealed class WpfClientContextService(IAppConfigService configService) : IClientContextService
{
    public ClientContext Current => new(
        configService.Current?.ProgramId,
        ResolveIpAddress(),
        Environment.UserName,
        AppContext.BaseDirectory);

    private static string? ResolveIpAddress()
    {
        try
        {
            var address = Dns.GetHostEntry(Dns.GetHostName()).AddressList
                .FirstOrDefault(x => x.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(x));
            return address?.ToString();
        }
        catch (SocketException)
        {
            return null;
        }
    }
}
