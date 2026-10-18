using System.Net;
using System.Net.Sockets;

namespace MmmToolWeb.Server.Hosting;

/// <summary>
/// 要求の接続元の扱い。
/// </summary>
internal static class ClientAddress
{
    /// <summary>接続元が分からないときの、回数制限の区分の名前。</summary>
    private const string Unknown = "unknown";

    /// <summary>
    /// 回数制限で、同じ接続元とみなす区分の名前を返す。
    /// </summary>
    /// <param name="context">今の要求。</param>
    /// <returns>区分の名前。IPv6 は、前半の 64 ビットで 1 つとみなす。</returns>
    /// <remarks>
    /// IPv6 は、1 つの回線に 64 ビットぶんの範囲のアドレスが割り当てられ、端末が範囲の中の別のアドレスへ変えられるため、アドレスそのものでは区別しない。
    /// </remarks>
    public static string PartitionKey(HttpContext context)
    {
        var address = context.Connection.RemoteIpAddress;
        if (address is null)
        {
            return Unknown;
        }
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }
        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            var bytes = address.GetAddressBytes();
            Array.Clear(bytes, 8, 8);
            return new IPAddress(bytes) + "/64";
        }
        return address.ToString();
    }
}
