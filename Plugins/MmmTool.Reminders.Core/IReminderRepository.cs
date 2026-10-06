using MmmSdk.Core.Components.Storage;

namespace MmmTool.Reminders.Core;

/// <summary>リマインダー (本体・対応状態)の保存先</summary>
/// <remarks>
/// 1 件単位の操作で書く (一覧を丸ごと置き換えない)。番号 (<see cref="Reminder.No"/>)と対応状態の連番 (<see cref="ReminderState.Seq"/>)は、保存先が「最大 + 1」で決める
/// (JSON ファイルはロックの中で計算し、SQL Server・DynamoDB などは、保存先の仕組みで一意になるように決める)。
/// 読み書きはスレッドセーフ。返す値は書き換えられない (<c>init</c> の record)ので、複製しなくてよい。
/// </remarks>
public interface IReminderRepository
{
    /// <summary>ファイルを読めなかったときのメッセージ。正常なら null</summary>
    /// <remarks>
    /// ロック・権限などで読めなかったとき (最初の読み込みのあとに分かる)。元のデータを上書きで消さないよう、このときは書き込みを止める (書こうとすると例外)。
    /// ファイルを使わない保存先は、常に null。
    /// </remarks>
    string? LoadError { get; }

    /// <summary>壊れていたファイルを退避して作り直したときのメッセージ。通常は null</summary>
    /// <remarks>ファイルを使わない保存先は、常に null。</remarks>
    string? RecoveryMessage { get; }

    /// <summary>リマインダー本体の一覧を取得する</summary>
    /// <param name="includeDeleted">論理削除済みも含めるか</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>リマインダー本体の一覧</returns>
    /// <remarks>保存先にまだ無い・空なら空の一覧。壊れていたときは退避してから空の一覧として扱う。</remarks>
    Task<IReadOnlyList<Reminder>> GetRemindersAsync(bool includeDeleted, CancellationToken cancellationToken = default);

    /// <summary>対応状態の一覧を取得する</summary>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>対応状態の一覧</returns>
    /// <remarks>保存先にまだ無い・空なら空の一覧。壊れていたときは退避してから空の一覧として扱う。</remarks>
    Task<IReadOnlyList<ReminderState>> GetStatesAsync(CancellationToken cancellationToken = default);

    /// <summary>リマインダーを追加する</summary>
    /// <param name="reminder">追加するリマインダー (<see cref="Reminder.No"/> は無視する)</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>追加した内容 (保存先が決めた番号つき)</returns>
    /// <exception cref="DataFileException">保存に失敗した・読み込みに失敗していて保存できない。</exception>
    Task<Reminder> AddAsync(Reminder reminder, CancellationToken cancellationToken = default);

    /// <summary>既存のリマインダーを、同じ番号の内容で置き換える</summary>
    /// <param name="reminder">置き換える内容 (<see cref="Reminder.No"/> で対象を決める)</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>対象があって置き換えたとき true (無ければ false)</returns>
    /// <exception cref="DataFileException">保存に失敗した・読み込みに失敗していて保存できない。</exception>
    Task<bool> UpdateAsync(Reminder reminder, CancellationToken cancellationToken = default);

    /// <summary>リマインダーの削除フラグ (論理削除)を変える</summary>
    /// <param name="no">対象のリマインダーの番号</param>
    /// <param name="isDeleted">削除済みにするか (false なら解除)</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>対象があれば true (すでに同じ値のときも true。このときは何も書かない)</returns>
    /// <exception cref="DataFileException">保存に失敗した・読み込みに失敗していて保存できない。</exception>
    Task<bool> SetDeletedAsync(int no, bool isDeleted, CancellationToken cancellationToken = default);

    /// <summary>リマインダーを物理削除する (本体と、その対応状態を、完全に取り除く)</summary>
    /// <param name="no">対象のリマインダーの番号</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>対象があって削除したとき true</returns>
    /// <remarks>
    /// 対応状態も一緒に消す。残すと、あとで同じ番号が採番されたとき (最大の番号を消した場合)に、無関係な新しいリマインダーが、前の状態 (完了など)を引き継いでしまうため。
    /// ファイルへの保存は、状態のファイルを先に書く (途中で失敗しても、状態が残るだけの側に倒す。本体が残って状態だけ消えるのは、未対応に戻るだけで害が小さい)。
    /// </remarks>
    /// <exception cref="DataFileException">保存に失敗した・読み込みに失敗していて保存できない。</exception>
    Task<bool> PurgeAsync(int no, CancellationToken cancellationToken = default);

    /// <summary>対応状態をまとめて保存する</summary>
    /// <param name="changes">変更する対応状態</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>保存の完了を表すタスク</returns>
    /// <remarks>同じリマインダー (<see cref="ReminderStateChange.BaseNo"/>)の状態は 1 件に保ち、あれば対象日・値を上書きする。無ければ連番を決めて追加する。1 回の書き込みで行う。</remarks>
    /// <exception cref="DataFileException">保存に失敗した・読み込みに失敗していて保存できない。</exception>
    Task SetStatesAsync(IReadOnlyList<ReminderStateChange> changes, CancellationToken cancellationToken = default);
}
