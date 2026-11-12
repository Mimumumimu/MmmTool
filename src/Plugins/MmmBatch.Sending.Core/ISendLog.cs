using MmmSdk.Core.Components.Storage;

namespace MmmBatch.Sending.Core;

/// <summary>送信のログの書き込み口</summary>
/// <remarks>
/// 送るたびに結果が決まったところで 1 行足す。
/// 再送・送り直しも 1 行ずつ増える。
/// あとから書き換えない。
/// アプリはログを読まない。
/// 画面に出さず、何かあったときに、人がファイルを読む。
/// 二重送信を防ぐ判断にも使わない。
/// 判断は各機能の DB の状態で行う。
/// </remarks>
public interface ISendLog
{
    /// <summary>ログに 1 行足す</summary>
    /// <param name="entry">足す行</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>書き込みの完了を表すタスク</returns>
    /// <exception cref="DataFileException">
    /// ファイルに書けなかった
    /// メッセージは画面に出せる
    /// </exception>
    Task AppendAsync(SendLogEntry entry, CancellationToken cancellationToken = default);

    /// <summary>古い日のログのファイルを old フォルダーへ移す</summary>
    /// <param name="today">今日</param>
    /// <returns>移したファイルの数</returns>
    /// <exception cref="DataFileException">
    /// ファイルを移せなかった
    /// メッセージは画面に出せる
    /// </exception>
    int MoveOldFiles(DateOnly today);
}
