using MmmSdk.Core.Components.Storage;

namespace MmmBatch.Sending.Core;

/// <summary>送信のログのファイルに書きながら、起動してから送った分をメモリにも持つ</summary>
/// <param name="file">ログのファイルに書く実装</param>
/// <remarks>
/// 画面の「履歴」用。
/// 書き込みは先にメモリへ足してから、ファイルに書く。
/// ファイルに書けなかったときも画面の履歴には出る。
/// 書けなかったことは呼び出し元へ例外で伝わる。
/// メモリは機能ごとに新しい <see cref="Capacity"/> 件まで持つ。
/// 増え続けないため。
/// ファイルは読まない。
/// </remarks>
public sealed class RecentSendLog(ISendLog file) : ISendLog, ISendHistory
{
    /// <summary>機能ごとにメモリに持つ最大の件数</summary>
    public const int Capacity = 100;

    /// <summary>メモリの更新を 1 つずつにするロック</summary>
    private readonly Lock _gate = new();

    /// <summary>機能のキーごとの記録</summary>
    private readonly Dictionary<string, LinkedList<SendLogEntry>> _bySource = [];

    /// <inheritdoc />
    /// <exception cref="DataFileException">
    /// ファイルに書けなかった
    /// メッセージは画面に出せる
    /// </exception>
    public Task AppendAsync(SendLogEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        lock (_gate)
        {
            if (!_bySource.TryGetValue(entry.Source, out var list))
            {
                list = new LinkedList<SendLogEntry>();
                _bySource[entry.Source] = list;
            }

            list.AddFirst(entry);
            while (list.Count > Capacity)
            {
                list.RemoveLast();
            }
        }

        return file.AppendAsync(entry, cancellationToken);
    }

    /// <inheritdoc />
    public int MoveOldFiles(DateOnly today) => file.MoveOldFiles(today);

    /// <inheritdoc />
    public IReadOnlyList<SendLogEntry> GetRecent(string source, int count)
    {
        ArgumentNullException.ThrowIfNull(source);

        lock (_gate)
        {
            return _bySource.TryGetValue(source, out var list) ? [.. list.Take(count)] : [];
        }
    }
}
