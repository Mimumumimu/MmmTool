using MmmSdk.Core.Components.Storage;

namespace MmmBatch.Sending.Core;

/// <summary>送信のログ (送った 1 回ごとの記録)の書き込み口と読み出し口</summary>
/// <remarks>
/// 送るたびに、結果が決まったところで 1 行足す。再送・送り直しも、1 行ずつ増える。あとから書き換えない。
/// ログは、二重送信を防ぐ判断には使わない (判断は <see cref="IReminderSendStatusRepository"/> の状態で行う)。
/// </remarks>
public interface ISendLog
{
    /// <summary>ログに 1 行足す</summary>
    /// <param name="entry">足す行</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>書き込みの完了を表すタスク</returns>
    /// <exception cref="DataFileException">ファイルに書けなかった (メッセージは画面に出せる)。</exception>
    Task AppendAsync(SendLogEntry entry, CancellationToken cancellationToken = default);

    /// <summary>新しい順に、ログを取得する</summary>
    /// <param name="count">取得する最大の件数</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>ログの行 (新しい順)。壊れた行は飛ばす</returns>
    /// <exception cref="DataFileException">ファイルを読めなかった (メッセージは画面に出せる)。</exception>
    Task<IReadOnlyList<SendLogEntry>> GetRecentAsync(int count, CancellationToken cancellationToken = default);

    /// <summary>古い日のログのファイルを、<c>old</c> フォルダーへ移す</summary>
    /// <param name="today">今日</param>
    /// <returns>移したファイルの数</returns>
    /// <exception cref="DataFileException">ファイルを移せなかった (メッセージは画面に出せる)。</exception>
    int MoveOldFiles(DateOnly today);
}
