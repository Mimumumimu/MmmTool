namespace MmmTool.Notes.Core;

/// <summary>
/// 他の人の作成・更新の通知の元になる、更新されたノート 1 件。
/// </summary>
/// <param name="Id">ノートの番号</param>
/// <param name="Title">タイトル</param>
/// <param name="EditorUserId">最後に更新した人 (<c>AppUser.Id</c>)</param>
/// <param name="CreatedAt">作成日時。作成の通知か更新の通知かを決めるために、読んだ値を比べるだけで、検索には使わない</param>
/// <param name="UpdatedAt">更新日時。読み始める基準になる</param>
/// <param name="StartDate">開始日。通知を押したときに開く日を決める</param>
/// <param name="EndDate">終了日。終わりがなければ <see cref="DateOnly.MaxValue"/></param>
public sealed record NoteChange(int Id, string Title, int EditorUserId, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, DateOnly StartDate, DateOnly EndDate)
{
    /// <summary>通知を押したときに開く日を決める</summary>
    /// <param name="today">今日</param>
    /// <returns>今日が期間内なら今日、そうでなければ開始日</returns>
    public DateOnly DayToOpen(DateOnly today) => StartDate <= today && today <= EndDate ? today : StartDate;
}

/// <summary>
/// 更新されたノートを読んだ結果。
/// </summary>
/// <param name="ServerNow">読んだ時点の、データベースサーバーの時刻。次に読む基準にする。読んだ側の PC の時計は使わない</param>
/// <param name="Changes">更新されたノート (更新日時の古い順)</param>
public sealed record NoteChanges(DateTimeOffset ServerNow, IReadOnlyList<NoteChange> Changes);
