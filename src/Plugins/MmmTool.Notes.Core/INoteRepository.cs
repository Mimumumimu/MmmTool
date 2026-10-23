namespace MmmTool.Notes.Core;

/// <summary>
/// ノート (<see cref="Note"/>)の保存先。ローカルモードは SQLite、DB モードは SQL Server。
/// </summary>
/// <remarks>
/// 読み書きは、操作した人から見えるノートだけが対象 (全員に見えるものと、自分が書いた「自分だけ」のもの)。見えないノートは、番号を指定しても、存在しないのと同じ (false を返す)。
/// 保存できなかったときは、画面に出せるメッセージの <see cref="MmmSdk.Core.Components.Storage.DataFileException"/> を投げる。
/// </remarks>
public interface INoteRepository
{
    /// <summary>複数の人で共有するか (DB モード)。false のとき、公開範囲・書いた人・更新者は出さない</summary>
    bool IsShared { get; }

    /// <summary>今のユーザーの番号 (ローカルモードは 1)</summary>
    /// <exception cref="InvalidOperationException">DB モードで、今のユーザーをまだ特定していない。</exception>
    int CurrentUserId { get; }

    /// <summary>指定の日に有効なノートを取得する</summary>
    /// <param name="day">日</param>
    /// <param name="includeDeleted">削除済みも含めるか</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>有効なノート (並びは不定。並べるのは呼ぶ側)</returns>
    /// <exception cref="MmmSdk.Core.Components.Storage.DataFileException">設定が足りない・接続できない・表が無い・読めなかった。</exception>
    Task<IReadOnlyList<Note>> GetOnDayAsync(DateOnly day, bool includeDeleted, CancellationToken cancellationToken = default);

    /// <summary>指定の日が実施日のノートを取得する (削除済みは含めない)</summary>
    /// <param name="day">日</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>実施日が、その日のノート (並びは不定)</returns>
    /// <exception cref="MmmSdk.Core.Components.Storage.DataFileException">設定が足りない・接続できない・表が無い・読めなかった。</exception>
    Task<IReadOnlyList<Note>> GetOnEventDateAsync(DateOnly day, CancellationToken cancellationToken = default);

    /// <summary>ノートを追加する</summary>
    /// <param name="input">入力内容 (<see cref="NoteInput.Validate"/> を通ったもの)</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>保存した内容 (保存先が決めた番号つき)</returns>
    /// <exception cref="ArgumentException">入力内容が正しくない。</exception>
    /// <exception cref="MmmSdk.Core.Components.Storage.DataFileException">保存できなかった。</exception>
    Task<Note> AddAsync(NoteInput input, CancellationToken cancellationToken = default);

    /// <summary>ノートを変更する</summary>
    /// <param name="id">ノートの番号</param>
    /// <param name="input">入力内容 (<see cref="NoteInput.Validate"/> を通ったもの)</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>変更したら true。見えない・存在しないときは false</returns>
    /// <exception cref="ArgumentException">入力内容が正しくない。</exception>
    /// <exception cref="MmmSdk.Core.Components.Storage.DataFileException">保存できなかった。</exception>
    Task<bool> UpdateAsync(int id, NoteInput input, CancellationToken cancellationToken = default);

    /// <summary>ノートを削除する・復元する (論理削除。行は消さない)</summary>
    /// <param name="id">ノートの番号</param>
    /// <param name="isDeleted">削除するなら true、復元するなら false</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>変更したら true。見えない・存在しないときは false</returns>
    /// <exception cref="MmmSdk.Core.Components.Storage.DataFileException">保存できなかった。</exception>
    Task<bool> SetDeletedAsync(int id, bool isDeleted, CancellationToken cancellationToken = default);
}
