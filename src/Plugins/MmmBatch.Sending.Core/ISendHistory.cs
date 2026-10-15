namespace MmmBatch.Sending.Core;

/// <summary>画面の「履歴」に出す、このアプリを起動してから送った分の記録 (メモリ上。起動時は空)</summary>
/// <remarks>
/// ログのファイル (<see cref="ISendLog"/>)は、アプリが読まない。履歴は、起動してから送った分だけを、メモリに持って出す (揮発性。アプリを閉じると消える)。
/// 古い分は、ログのファイルを人が読む。
/// </remarks>
public interface ISendHistory
{
    /// <summary>新しい順に、機能ごとの履歴を取得する</summary>
    /// <param name="source">取得する機能のキー (<see cref="SendLogEntry.Source"/>)。この機能の行だけを返す</param>
    /// <param name="count">取得する最大の件数</param>
    /// <returns>起動してから送った分の記録 (新しい順)。まだ送っていなければ空</returns>
    IReadOnlyList<SendLogEntry> GetRecent(string source, int count);
}
