using MmmTool.Users.Core;

namespace MmmToolWeb.Features;

/// <summary>
/// 1 つの要求の中で、今のユーザーの機能の状態を、1 回だけ読んで共有する。
/// </summary>
/// <param name="access">機能の利用。</param>
/// <param name="currentUser">今のユーザー。</param>
/// <remarks>
/// 要求ごとに作られる。機能の入口のフィルターと、機能の API の中の判定が、同じ要求で別々に読まないために使う。
/// 管理者の変更は、次の要求から反映される。要求をまたぐ控えは持たない。
/// </remarks>
public sealed class RequestFeatureAccess(FeatureAccessService access, CurrentUser currentUser)
{
    /// <summary>読み込んだ状態。まだ読んでいなければ null。</summary>
    private Task<IReadOnlyDictionary<int, FeatureAccess>>? _states;

    /// <summary>
    /// 今のユーザーの、すべての機能の状態を取得する。
    /// </summary>
    /// <param name="cancellationToken">キャンセルの通知。</param>
    /// <returns>機能番号と状態の組。行が無い機能は含まず、有効として扱う。</returns>
    /// <remarks>最初の呼び出しだけ DB を読む。</remarks>
    public Task<IReadOnlyDictionary<int, FeatureAccess>> GetAllAsync(CancellationToken cancellationToken = default)
        => _states ??= access.GetAllAsync(currentUser.Id, cancellationToken);

    /// <summary>
    /// 管理者に、その機能を無効にされているかを調べる。
    /// </summary>
    /// <param name="featureNo">機能番号。</param>
    /// <param name="cancellationToken">キャンセルの通知。</param>
    /// <returns>無効のとき true。</returns>
    public async Task<bool> IsDisabledAsync(int featureNo, CancellationToken cancellationToken = default)
        => (await GetAllAsync(cancellationToken)).TryGetValue(featureNo, out var state) && state == FeatureAccess.Disabled;
}
