namespace MmmTool.Data.Queries;

/// <summary>Repository が接続を開いて処理を実行する入口</summary>
/// <remarks>
/// DB の種類ごとに、接続の開き方・書き込みのトランザクション・ユーザーの確かめ方・処理を動かすスレッドが違う。
/// その違いを、種類ごとのプロジェクトの <see cref="DbGateway"/> に残す。
/// </remarks>
public abstract class DbGateway
{
    /// <summary>DB の種類ごとの SQL の書き方</summary>
    public abstract SqlDialect Dialect { get; }

    /// <summary>今のユーザーの番号</summary>
    /// <remarks>
    /// 特定していないときは 0
    /// 読み込みは何も返さない
    /// </remarks>
    public abstract int UserId { get; }

    /// <summary>ユーザーを特定していることを確かめる</summary>
    /// <returns>今のユーザーの番号</returns>
    /// <exception cref="MmmSdk.Core.Components.Storage.DataFileException">
    /// ユーザーを特定していない
    /// ログインが必要
    /// </exception>
    public abstract int RequireUserId();

    /// <summary>接続を開いて読む処理を実行する</summary>
    /// <typeparam name="T">処理の結果の型</typeparam>
    /// <param name="work">開いた接続で行う処理</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>処理の結果</returns>
    public abstract Task<T> ReadAsync<T>(Func<DbSession, Task<T>> work, CancellationToken cancellationToken);

    /// <summary>トランザクションの中で書く処理を実行する</summary>
    /// <param name="work">開いた接続・トランザクション・今のユーザーで行う書き込み</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>書き込みの完了を表すタスク</returns>
    /// <remarks>
    /// 処理が最後まで終わったときだけ、確定する。
    /// ユーザーを特定していなければ、何も書かず <see cref="MmmSdk.Core.Components.Storage.DataFileException"/> を投げる。
    /// </remarks>
    public abstract Task WriteAsync(Func<DbSession, Task> work, CancellationToken cancellationToken);
}
