namespace MmmTool.Users.Core;

/// <summary>
/// ユーザーごとの、機能の利用の状態 (<see cref="FeatureAccess"/>)を読み書きする。
/// </summary>
/// <param name="repository">管理者が決める設定の保存先</param>
/// <remarks>
/// 保存は <c>AppUserAdminSetting</c> の区分 <see cref="CategoryNo"/>・コード番号 <see cref="CodeNo"/>。行が無い人と、読めない値は <see cref="FeatureAccess.Enabled"/>。
/// ローカルモード (<see cref="IsAvailable"/> が false)では、読む操作はすべて有効を返し、保存は <see cref="NotSupportedException"/>。
/// </remarks>
public sealed class FeatureAccessService(IAppUserAdminSettingRepository repository)
{
    /// <summary>機能の利用の区分番号</summary>
    public const int CategoryNo = 1;

    /// <summary>機能の利用のコード番号</summary>
    public const int CodeNo = 1;

    /// <summary>機能の利用を使える保存先か (DB モードのとき true)</summary>
    public bool IsAvailable => repository.IsAvailable;

    /// <summary>保存する値 (文字列)から、状態を決める</summary>
    /// <param name="value">保存された値。null (行が無い)も渡せる</param>
    /// <returns>状態。null・読めない値・範囲外の値は <see cref="FeatureAccess.Enabled"/></returns>
    public static FeatureAccess Parse(string? value)
        => int.TryParse(value, out var number) && Enum.IsDefined((FeatureAccess)number) ? (FeatureAccess)number : FeatureAccess.Enabled;

    /// <summary>1 人の、1 つの機能の状態を取得する</summary>
    /// <param name="userId">ユーザーの番号</param>
    /// <param name="featureNo">機能番号</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>状態。ローカルモードは <see cref="FeatureAccess.Enabled"/></returns>
    /// <exception cref="MmmSdk.Core.Components.Storage.DataFileException">接続できない・表が無い・読めなかった。メッセージは画面に出せる。</exception>
    public async Task<FeatureAccess> GetAsync(int userId, int featureNo, CancellationToken cancellationToken = default)
        => IsAvailable
            ? Parse(await repository.GetAsync(userId, featureNo, CategoryNo, CodeNo, cancellationToken).ConfigureAwait(false))
            : FeatureAccess.Enabled;

    /// <summary>1 人の、すべての機能の状態を取得する</summary>
    /// <param name="userId">ユーザーの番号</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>機能番号と状態の組。行が無い機能は含まない (有効として扱う)</returns>
    /// <exception cref="MmmSdk.Core.Components.Storage.DataFileException">接続できない・表が無い・読めなかった。メッセージは画面に出せる。</exception>
    public async Task<IReadOnlyDictionary<int, FeatureAccess>> GetAllAsync(int userId, CancellationToken cancellationToken = default)
    {
        if (!IsAvailable)
        {
            return new Dictionary<int, FeatureAccess>();
        }

        var values = await repository.GetByFeatureAsync(userId, CategoryNo, CodeNo, cancellationToken).ConfigureAwait(false);
        return values.ToDictionary(pair => pair.Key, pair => Parse(pair.Value));
    }

    /// <summary>1 人の、1 つの機能の状態を保存する</summary>
    /// <param name="userId">ユーザーの番号</param>
    /// <param name="featureNo">機能番号</param>
    /// <param name="access">状態</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>保存の完了を表すタスク</returns>
    /// <exception cref="NotSupportedException">ローカルモード。</exception>
    /// <exception cref="MmmSdk.Core.Components.Storage.DataFileException">接続できない・表が無い・保存できなかった。メッセージは画面に出せる。</exception>
    public Task SetAsync(int userId, int featureNo, FeatureAccess access, CancellationToken cancellationToken = default)
        => repository.SetAsync(userId, featureNo, CategoryNo, CodeNo, ((int)access).ToString(), cancellationToken);
}
