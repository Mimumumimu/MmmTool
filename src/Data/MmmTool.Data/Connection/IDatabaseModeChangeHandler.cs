namespace MmmTool.Data.Connection;

/// <summary>保存先のモードが変わったときにモードに依存するユーザー設定を初期化する</summary>
/// <remarks>
/// 案件の番号のように、保存先ごとに意味が変わる値を持つ設定はモードを切り替えると合わなくなる。
/// モードの切り替えは想定しない (デバッグで切り替える程度)ので、合わなくなる設定は切り替えのときに消す。
/// ユーザー設定なので、消えてよい。
/// 機能ごとに実装し、<c>MmmTool.Data.Storage</c> が DI に登録する。
/// </remarks>
public interface IDatabaseModeChangeHandler
{
    /// <summary>モードが変わった</summary>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>初期化の完了を表すタスク</returns>
    Task OnModeChangedAsync(CancellationToken cancellationToken = default);
}
