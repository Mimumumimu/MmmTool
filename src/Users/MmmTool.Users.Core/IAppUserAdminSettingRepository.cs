namespace MmmTool.Users.Core;

/// <summary>
/// 管理者がユーザーごとに決める設定 (<c>dbo.AppUserAdminSetting</c>)の保存先。人・機能・区分・コード番号ごとに、値を 1 件ずつ持つ縦持ちの表。
/// </summary>
/// <remarks>
/// DB モードだけで使う。ローカルモードでは、<see cref="IsAvailable"/> が false の実装 (<see cref="UnavailableAppUserAdminSettingRepository"/>)が入る。
/// 書くのは管理ツールだけで、更新者の列には 0 (特定のユーザーではない)が入る。ほかのアプリは読むだけ。
/// 操作するのは、引数で渡した人の設定。今のユーザーには限らない。
/// 機能番号は、<c>CodeMaster</c> と同じ台帳 (<c>CodeFeatures</c>)の番号。
/// </remarks>
public interface IAppUserAdminSettingRepository
{
    /// <summary>使える保存先か</summary>
    /// <remarks>DB モードは true、ローカルモードは false。false のとき、ほかの操作は <see cref="NotSupportedException"/>。</remarks>
    bool IsAvailable { get; }

    /// <summary>設定の値を取得する</summary>
    /// <param name="userId">設定を持つ人の番号</param>
    /// <param name="featureNo">機能番号</param>
    /// <param name="categoryNo">区分番号</param>
    /// <param name="codeNo">コード番号 (枝番)</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>値。設定が無いときは null</returns>
    /// <exception cref="MmmSdk.Core.Components.Storage.DataFileException">設定が足りない・接続できない・表が無い・読めなかった。メッセージは画面に出せる。</exception>
    Task<string?> GetAsync(int userId, int featureNo, int categoryNo, int codeNo, CancellationToken cancellationToken = default);

    /// <summary>1 人の、同じ区分・コード番号の設定を、機能ごとに取得する</summary>
    /// <param name="userId">設定を持つ人の番号</param>
    /// <param name="categoryNo">区分番号</param>
    /// <param name="codeNo">コード番号 (枝番)</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>機能番号と値の組。設定が無い機能は含まない</returns>
    /// <exception cref="MmmSdk.Core.Components.Storage.DataFileException">設定が足りない・接続できない・表が無い・読めなかった。メッセージは画面に出せる。</exception>
    Task<IReadOnlyDictionary<int, string>> GetByFeatureAsync(int userId, int categoryNo, int codeNo, CancellationToken cancellationToken = default);

    /// <summary>設定の値を保存する</summary>
    /// <param name="userId">設定を持つ人の番号</param>
    /// <param name="featureNo">機能番号</param>
    /// <param name="categoryNo">区分番号</param>
    /// <param name="codeNo">コード番号 (枝番)</param>
    /// <param name="value">値</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>保存の完了を表すタスク</returns>
    /// <remarks>設定があれば値を直し、外した行があれば削除を戻して値を直し、無ければ足す。</remarks>
    /// <exception cref="ArgumentException">番号が範囲外・値が長すぎる。</exception>
    /// <exception cref="MmmSdk.Core.Components.Storage.DataFileException">設定が足りない・接続できない・表が無い・保存できなかった。メッセージは画面に出せる。</exception>
    Task SetAsync(int userId, int featureNo, int categoryNo, int codeNo, string value, CancellationToken cancellationToken = default);
}
