namespace MmmTool.Users.Core;

/// <summary>
/// 今のユーザーの、機能の利用の状態 (<see cref="FeatureAccess"/>)を、メモリに持つ。
/// </summary>
/// <param name="currentUser">今のユーザー</param>
/// <param name="service">機能の利用の読み書き</param>
/// <remarks>
/// サイドバーのような同期の判定が、DB を待たずに読めるようにするための控え。判定は <see cref="Get"/> で行う。
/// 読み込むのは、ユーザーを特定したあとの <see cref="RefreshAsync"/>。読み込むまでと、ローカルモードでは、すべて有効。
/// 管理者の変更は、起動のときの読み込みから反映される。
/// </remarks>
public sealed class FeatureAccessCache(CurrentUser currentUser, FeatureAccessService service)
{
    private volatile IReadOnlyDictionary<int, FeatureAccess> _values = new Dictionary<int, FeatureAccess>();

    /// <summary>控えを読み込み直したとき</summary>
    /// <remarks>読み込んだスレッドから発火する。任意のスレッドになりうる。</remarks>
    public event EventHandler? Changed;

    /// <summary>機能の状態を取得する</summary>
    /// <param name="featureNo">機能番号</param>
    /// <returns>状態。控えに無い機能は <see cref="FeatureAccess.Enabled"/></returns>
    public FeatureAccess Get(int featureNo)
        => _values.TryGetValue(featureNo, out var access) ? access : FeatureAccess.Enabled;

    /// <summary>今のユーザーの状態を読み込み直す</summary>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>読み込みの完了を表すタスク</returns>
    /// <remarks>ローカルモードと、ユーザーを特定していないときは、控えを空にして、すべて有効にする。</remarks>
    /// <exception cref="MmmSdk.Core.Components.Storage.DataFileException">接続できない・表が無い・読めなかった。控えは、前の内容のまま。メッセージは画面に出せる。</exception>
    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        _values = service.IsAvailable && currentUser.IsIdentified
            ? await service.GetAllAsync(currentUser.Id, cancellationToken).ConfigureAwait(false)
            : new Dictionary<int, FeatureAccess>();
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
