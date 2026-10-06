using System.Net.NetworkInformation;

namespace MmmTool.Users.Core;

/// <summary>
/// ネットワークアダプター (イーサネット・無線)から、この PC の MAC アドレスを取得する。
/// </summary>
/// <remarks>
/// ループバック・トンネルなどは含めない。仮想のアダプターも、種類がイーサネットなら含まれうる
/// (ユーザーを特定するときは、登録済みの MAC のどれかに合えばよく、登録するときは一覧から選ぶ)。
/// </remarks>
public sealed class NetworkMacAddressProvider : IMacAddressProvider
{
    /// <inheritdoc />
    public IReadOnlyList<string> GetMacAddresses() =>
    [
        .. NetworkInterface.GetAllNetworkInterfaces()
            .Where(adapter => adapter.NetworkInterfaceType is
                NetworkInterfaceType.Ethernet or NetworkInterfaceType.GigabitEthernet
                or NetworkInterfaceType.FastEthernetT or NetworkInterfaceType.FastEthernetFx
                or NetworkInterfaceType.Wireless80211)
            .Select(adapter => (IsUp: adapter.OperationalStatus == OperationalStatus.Up, Mac: adapter.GetPhysicalAddress().ToString()))
            .Where(item => item.Mac.Length == 12 && item.Mac.Any(character => character != '0'))
            .OrderByDescending(item => item.IsUp)
            .Select(item => item.Mac)
            .Distinct(),
    ];
}
