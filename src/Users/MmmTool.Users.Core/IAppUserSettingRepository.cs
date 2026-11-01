namespace MmmTool.Users.Core;

/// <summary>
/// ユーザーごとの設定 (<c>dbo.AppUserSetting</c>)の保存先。人・機能・区分・コード番号ごとに、値を 1 件ずつ持つ縦持ちの表。
/// </summary>
/// <remarks>
/// DB モードだけで使う。ローカルモードでは、<see cref="IsAvailable"/> が false の実装 (<see cref="UnavailableAppUserSettingRepository"/>)が入る。
/// 値は文字列で持ち、整数は読むときに直す。操作するのは今のユーザー自身の設定だけで、ほかの人の設定は、必要な機能が問い合わせの中で読む。
/// 機能番号は、<c>CodeMaster</c> と同じ台帳 (<c>CodeFeatures</c>)の番号。
/// </remarks>
public interface IAppUserSettingRepository
{
    /// <summary>使える保存先か</summary>
    /// <remarks>DB モードは true、ローカルモードは false。false のとき、ほかの操作は <see cref="NotSupportedException"/>。</remarks>
    bool IsAvailable { get; }

    /// <summary>今のユーザーの設定の値を取得する</summary>
    /// <param name="featureNo">機能番号</param>
    /// <param name="categoryNo">区分番号</param>
    /// <param name="codeNo">コード番号 (枝番)</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>値。設定が無い (外した)ときは null</returns>
    /// <exception cref="MmmSdk.Core.Components.Storage.DataFileException">ユーザーを特定していない・設定が足りない・接続できない・表が無い・読めなかった。メッセージは画面に出せる。</exception>
    Task<string?> GetAsync(int featureNo, int categoryNo, int codeNo, CancellationToken cancellationToken = default);

    /// <summary>今のユーザーの、同じ区分・コード番号の設定を、機能ごとに 1 回で取得する</summary>
    /// <param name="categoryNo">区分番号</param>
    /// <param name="codeNo">コード番号 (枝番)</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>機能番号と値の組。設定が無い (外した)機能は含まない</returns>
    /// <remarks>機能の数だけ <see cref="GetAsync"/> を呼ぶ代わりに使う。</remarks>
    /// <exception cref="MmmSdk.Core.Components.Storage.DataFileException">ユーザーを特定していない・設定が足りない・接続できない・表が無い・読めなかった。メッセージは画面に出せる。</exception>
    Task<IReadOnlyDictionary<int, string>> GetByFeatureAsync(int categoryNo, int codeNo, CancellationToken cancellationToken = default);

    /// <summary>今のユーザーの設定の値を保存する</summary>
    /// <param name="featureNo">機能番号</param>
    /// <param name="categoryNo">区分番号</param>
    /// <param name="codeNo">コード番号 (枝番)</param>
    /// <param name="value">値</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>保存の完了を表すタスク</returns>
    /// <remarks>設定があれば値を直し、外した行があれば削除を戻して値を直し、無ければ足す。</remarks>
    /// <exception cref="ArgumentException">番号が 1 未満・値が長すぎる。</exception>
    /// <exception cref="MmmSdk.Core.Components.Storage.DataFileException">ユーザーを特定していない・設定が足りない・接続できない・表が無い・保存できなかった。メッセージは画面に出せる。</exception>
    Task SetAsync(int featureNo, int categoryNo, int codeNo, string value, CancellationToken cancellationToken = default);

    /// <summary>今のユーザーの設定を外す (論理削除する)</summary>
    /// <param name="featureNo">機能番号</param>
    /// <param name="categoryNo">区分番号</param>
    /// <param name="codeNo">コード番号 (枝番)</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>外す処理の完了を表すタスク。設定が無くても、失敗にしない</returns>
    /// <exception cref="MmmSdk.Core.Components.Storage.DataFileException">ユーザーを特定していない・設定が足りない・接続できない・表が無い・保存できなかった。メッセージは画面に出せる。</exception>
    Task ClearAsync(int featureNo, int categoryNo, int codeNo, CancellationToken cancellationToken = default);
}
