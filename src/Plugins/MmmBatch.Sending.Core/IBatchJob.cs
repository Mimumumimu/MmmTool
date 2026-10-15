namespace MmmBatch.Sending.Core;

/// <summary><see cref="BatchMonitor"/> が、仕事ごとの間隔で動かす仕事 (バッチ処理 1 つ分)</summary>
/// <remarks>
/// 各機能が 1 つずつ実装し、<see cref="SendingCoreServiceCollectionExtensions.AddBatchJob{TJob}"/> で登録する。ホストは、機能の名前を持たずに、登録された仕事を動かす。
/// DB の読み書きの失敗は、仕事が自分で受け止めて <see cref="LastError"/> に残す (ほかの仕事を止めない)。
/// </remarks>
public interface IBatchJob
{
    /// <summary>仕事の名前 (ログ・画面に出す)</summary>
    string Name { get; }

    /// <summary>動かす間隔</summary>
    /// <remarks>0 時からの区切りに合わせて動く (毎分なら毎分 00 秒)。時刻で送る仕事は 1 分、依頼をすぐ送る仕事は数秒にする。1 日を割り切れる値にすること。</remarks>
    TimeSpan Interval { get; }

    /// <summary>最後の実行の失敗 (DB の読み書きができなかった)。無ければ null</summary>
    /// <remarks>次の実行が成功すると、消える。変わったときも <see cref="Changed"/> が発火する。</remarks>
    string? LastError { get; }

    /// <summary>仕事の結果が変わった (送った・失敗した・送り直しを始めた・失敗の内容が変わった)</summary>
    /// <remarks>任意のスレッドから発火する。</remarks>
    event EventHandler? Changed;

    /// <summary>実行が終わった (変わったことが無くても発火する)</summary>
    /// <remarks>画面が、実行のたびに読み直すために使う (間隔が長い仕事の一覧だけ)。任意のスレッドから発火する。</remarks>
    event EventHandler? Ran;

    /// <summary>今の時刻に動かす</summary>
    /// <param name="now">今の日時 (ローカル)</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>実行の完了を表すタスク</returns>
    Task RunAsync(DateTime now, CancellationToken cancellationToken = default);
}
