using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace MAP.C.Contract.Models;

public sealed class SystemInfo
{
    public string MachineName { get; }
    public string IpAddress { get; }
    public string OsVersion { get; }
    public string DotNetVersion { get; }
    public string UserName { get; }

    public SystemInfo()
    {
        MachineName = Environment.MachineName;
        UserName = Environment.UserName;
        OsVersion = RuntimeInformation.OSDescription;
        DotNetVersion = RuntimeInformation.FrameworkDescription;
        IpAddress = GetLocalIpAddress();
    }

    private static string GetLocalIpAddress()
    {
        try
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
                    return ip.ToString();
            }
        }
        catch (SocketException) { }
        catch (Exception) { }
        return "Unknown";
    }
}
