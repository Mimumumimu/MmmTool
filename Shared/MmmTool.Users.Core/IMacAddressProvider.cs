namespace MmmTool.Users.Core;

/// <summary>
/// この PC の MAC アドレスを取得する。
/// </summary>
public interface IMacAddressProvider
{
    /// <summary>この PC のネットワークアダプターの MAC アドレスの一覧を取得する (ユーザーを特定するときに使う)</summary>
    /// <returns>大文字の 16 進 12 桁・区切りなしの MAC アドレス。使える状態 (接続中)のものが先。重複はなく、無ければ空</returns>
    /// <remarks>仮想のアダプターも含める (登録済みの MAC のどれかに合えばよいため)。</remarks>
    IReadOnlyList<string> GetMacAddresses();

    /// <summary>登録の画面で選ばせる、アダプターの一覧を取得する</summary>
    /// <returns>仮想らしくないアダプター。1 つも無いときは、<see cref="GetMacAddresses"/> と同じ範囲のすべて (登録できなくならないため)。接続中のものが先</returns>
    IReadOnlyList<MacAdapter> GetRegistrationAdapters();
}
