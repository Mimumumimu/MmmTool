using System.Net.NetworkInformation;

namespace MmmTool.Users.Core;

/// <summary>
/// ネットワークアダプター (イーサネット・無線)から、この PC の MAC アドレスを取得する。
/// </summary>
/// <remarks>
/// ループバック・トンネル・Bluetooth などの種類は含めない。ユーザーを特定するときは、仮想のアダプターも含める (登録済みの MAC のどれかに合えばよい)。
/// 登録の画面で選ばせるのは、仮想らしくないものだけ (仮想のアダプターは、MAC が変わりやすく、特定が安定しないため)。仮想らしいかは、名前に特定の言葉を含むか、
/// MAC が「ローカル管理」(OS やソフトが決めた値。先頭バイトの下から 2 番目のビット)かで見分ける。見分けは目安なので、1 つも残らないときは、すべてを出す。
/// </remarks>
public sealed class NetworkMacAddressProvider : IMacAddressProvider
{
    /// <summary>仮想・ネットワークの口ではないものの、名前に含まれる言葉 (大文字・小文字は区別しない)。ほかの PC で見分け損ねたものは、ここに足す</summary>
    private static readonly string[] VirtualNameWords = ["Virtual", "Bluetooth", "VPN", "TAP-", "Hyper-V", "VMware"];

    /// <summary>MAC の先頭バイトで、「ローカル管理」を表すビット</summary>
    private const int LocallyAdministeredBit = 0x02;

    /// <inheritdoc />
    public IReadOnlyList<string> GetMacAddresses() => [.. GetAdapters().Select(adapter => adapter.MacAddress).Distinct()];

    /// <inheritdoc />
    public IReadOnlyList<MacAdapter> GetRegistrationAdapters()
    {
        var adapters = GetAdapters();
        var preferred = adapters.Where(adapter => !adapter.LooksVirtual).ToList();
        return [.. (preferred.Count > 0 ? preferred : adapters)
            .DistinctBy(adapter => adapter.MacAddress)
            .Select(adapter => new MacAdapter(adapter.MacAddress, adapter.Name))];
    }

    /// <summary>種類で絞ったアダプターを、接続中のもの・仮想らしくないものが先になる順で取得する</summary>
    /// <returns>アダプターの一覧 (MAC が正しいものだけ)</returns>
    private static List<Adapter> GetAdapters() =>
    [
        .. NetworkInterface.GetAllNetworkInterfaces()
            .Where(adapter => adapter.NetworkInterfaceType is
                NetworkInterfaceType.Ethernet or NetworkInterfaceType.GigabitEthernet
                or NetworkInterfaceType.FastEthernetT or NetworkInterfaceType.FastEthernetFx
                or NetworkInterfaceType.Wireless80211)
            .Select(adapter => (Mac: adapter.GetPhysicalAddress().ToString(), adapter.Description, IsUp: adapter.OperationalStatus == OperationalStatus.Up))
            .Where(adapter => adapter.Mac.Length == 12 && adapter.Mac.Any(character => character != '0'))
            .Select(adapter => new Adapter(adapter.Mac, adapter.Description, adapter.IsUp))
            .OrderByDescending(adapter => adapter.IsUp)
            .ThenBy(adapter => adapter.LooksVirtual),
    ];

    /// <summary>絞り込みの途中で使う、アダプター 1 つ</summary>
    /// <param name="MacAddress">MAC アドレス (大文字の 16 進 12 桁・区切りなし)</param>
    /// <param name="Name">Windows が出す説明</param>
    /// <param name="IsUp">接続中か</param>
    private sealed record Adapter(string MacAddress, string Name, bool IsUp)
    {
        /// <summary>仮想らしいか (名前に特定の言葉を含む、または MAC がローカル管理)</summary>
        public bool LooksVirtual { get; } =
            VirtualNameWords.Any(word => Name.Contains(word, StringComparison.OrdinalIgnoreCase))
            || (Convert.ToInt32(MacAddress[..2], 16) & LocallyAdministeredBit) != 0;
    }
}
