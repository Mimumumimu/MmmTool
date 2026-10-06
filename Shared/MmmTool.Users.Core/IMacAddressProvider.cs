namespace MmmTool.Users.Core;

/// <summary>
/// この PC の MAC アドレスを取得する。
/// </summary>
public interface IMacAddressProvider
{
    /// <summary>この PC のネットワークアダプターの MAC アドレスの一覧を取得する</summary>
    /// <returns>大文字の 16 進 12 桁・区切りなしの MAC アドレス。使える状態 (接続中)のものが先。重複はなく、無ければ空</returns>
    IReadOnlyList<string> GetMacAddresses();
}
