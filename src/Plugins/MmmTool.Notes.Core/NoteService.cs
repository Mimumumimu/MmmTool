namespace MmmTool.Notes.Core;

/// <summary>ノートの読み書きの入口</summary>
/// <param name="repository">ノートの保存先</param>
/// <param name="time">現在時刻の取得元</param>
/// <remarks>
/// 並び・名前の出し方・今日の更新の印を決める。
/// 並びは表示する日が実施日のカードが先頭で、そのあとは重要度の高い順。
/// 同じ重要度の中は更新日時の新しい順で、同じ更新日時なら番号の大きい順。
/// 作成者・更新者の名前は保存先がノートと一緒に 1 回の問い合わせで読み、<see cref="NoteWithNames"/> で受け取る。
/// 名前を別に引かない。
/// </remarks>
public sealed class NoteService(INoteRepository repository, TimeProvider time)
{
    /// <summary>名前が引けなかったユーザーの表示</summary>
    public const string UnknownUserName = "(不明なユーザー)";

    /// <summary>複数の人で共有するか</summary>
    public bool IsShared => repository.IsShared;

    /// <summary>今のユーザーの番号</summary>
    /// <exception cref="InvalidOperationException">DB モードで、今のユーザーをまだ特定していない。</exception>
    public int CurrentUserId => repository.CurrentUserId;

    /// <summary>実際の今日</summary>
    public DateOnly Today => DateOnly.FromDateTime(time.GetLocalNow().DateTime);

    /// <summary>指定の日のノートを基準に従って並べて取得する</summary>
    /// <param name="day">日</param>
    /// <param name="basis">その日にどのノートを出すかの基準</param>
    /// <param name="includeDeleted">削除済みも含めるか</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>
    /// カードに出す内容。
    /// <see cref="NoteBasis.Period"/> は実施日のカードが先頭、そのあとは重要度の高い順、同じ重要度の中は更新日時の新しい順。
    /// <see cref="NoteBasis.Updated"/> は更新日時の新しい順で、同じなら番号の大きい順。
    /// </returns>
    /// <exception cref="MmmSdk.Core.Components.Storage.DataFileException">
    /// 読めなかった
    /// メッセージは画面に出せる
    /// </exception>
    /// <remarks>
    /// <see cref="NoteBasis.Updated"/> の「その日」はこの PC の時刻での、その日の 0 時から翌日の 0 時まで。
    /// ノートは変更の履歴を持たないので、出るのは「最後に更新した日がその日」のノートだけ。
    /// </remarks>
    public async Task<IReadOnlyList<NoteCard>> LoadAsync(DateOnly day, NoteBasis basis, bool includeDeleted, CancellationToken cancellationToken = default)
    {
        List<NoteWithNames> sorted;
        if (basis == NoteBasis.Updated)
        {
            var notes = await repository.GetUpdatedBetweenAsync(StartOfDay(day), StartOfDay(day.AddDays(1)), includeDeleted, cancellationToken).ConfigureAwait(false);
            sorted = [.. notes.OrderByDescending(item => item.Note.UpdatedAt).ThenByDescending(item => item.Note.Id)];
        }
        else
        {
            var notes = await repository.GetOnDayAsync(day, includeDeleted, cancellationToken).ConfigureAwait(false);
            sorted =
            [
                .. notes
                    .OrderByDescending(item => item.Note.IsEventOn(day))
                    .ThenByDescending(item => item.Note.Priority)
                    .ThenByDescending(item => item.Note.UpdatedAt)
                    .ThenByDescending(item => item.Note.Id),
            ];
        }

        var today = Today;
        return [.. sorted.Select(item => new NoteCard(
            item.Note,
            NameOf(item.AuthorName),
            NameOf(item.UpdatedByName),
            DateOnly.FromDateTime(item.Note.UpdatedAt.ToLocalTime().DateTime) == today,
            item.Note.IsEventOn(day),
            day == today))];
    }

    /// <summary>実際の今日が実施日のノートを並べて取得する</summary>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>削除されていない、見えるノート (重要度の高い順、同じ重要度の中は更新日時の新しい順)。</returns>
    /// <remarks>通知ダイアログに出す</remarks>
    /// <exception cref="MmmSdk.Core.Components.Storage.DataFileException">
    /// 読めなかった
    /// メッセージは画面に出せる
    /// </exception>
    public async Task<IReadOnlyList<Note>> LoadEventsTodayAsync(CancellationToken cancellationToken = default)
    {
        var notes = await repository.GetOnEventDateAsync(Today, cancellationToken).ConfigureAwait(false);
        return [.. notes.OrderByDescending(note => note.Priority).ThenByDescending(note => note.UpdatedAt).ThenByDescending(note => note.Id)];
    }

    /// <summary>ノートを追加する</summary>
    /// <param name="input">入力内容</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>保存した内容</returns>
    /// <exception cref="ArgumentException">
    /// 入力内容が正しくない
    /// <see cref="NoteInput.Validate"/> で先に調べる
    /// </exception>
    /// <exception cref="MmmSdk.Core.Components.Storage.DataFileException">
    /// 保存できなかった
    /// メッセージは画面に出せる
    /// </exception>
    public Task<Note> AddAsync(NoteInput input, CancellationToken cancellationToken = default)
        => repository.AddAsync(Checked(input), cancellationToken);

    /// <summary>ノートを変更する</summary>
    /// <param name="id">ノートの番号</param>
    /// <param name="input">入力内容</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>
    /// 変更したら true
    /// 見えない・存在しないときは false
    /// </returns>
    /// <exception cref="ArgumentException">入力内容が正しくない</exception>
    /// <exception cref="MmmSdk.Core.Components.Storage.DataFileException">
    /// 保存できなかった
    /// メッセージは画面に出せる
    /// </exception>
    public Task<bool> UpdateAsync(int id, NoteInput input, CancellationToken cancellationToken = default)
        => repository.UpdateAsync(id, Checked(input), cancellationToken);

    /// <summary>ノートを削除する・復元する</summary>
    /// <param name="id">ノートの番号</param>
    /// <param name="isDeleted">削除するなら true、復元するなら false。</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>
    /// 変更したら true
    /// 見えない・存在しないときは false
    /// </returns>
    /// <exception cref="MmmSdk.Core.Components.Storage.DataFileException">
    /// 保存できなかった
    /// メッセージは画面に出せる
    /// </exception>
    public Task<bool> SetDeletedAsync(int id, bool isDeleted, CancellationToken cancellationToken = default)
        => repository.SetDeletedAsync(id, isDeleted, cancellationToken);

    /// <summary>この PC の時刻でのその日の 0 時</summary>
    /// <param name="day">日</param>
    /// <returns>その日の 0 時 (この PC の時差つき)</returns>
    private DateTimeOffset StartOfDay(DateOnly day)
    {
        var local = day.ToDateTime(TimeOnly.MinValue);
        return new DateTimeOffset(local, time.LocalTimeZone.GetUtcOffset(local));
    }

    /// <summary>入力内容を調べて前後の空白を取り除く</summary>
    /// <param name="input">入力内容</param>
    /// <returns>保存する内容</returns>
    /// <exception cref="ArgumentException">入力内容が正しくない</exception>
    private static NoteInput Checked(NoteInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.Validate() is { } error)
        {
            throw new ArgumentException(error, nameof(input));
        }
        return input.Trimmed();
    }

    /// <summary>カードに出す名前にする</summary>
    /// <param name="name">
    /// 保存先が読んだ表示名
    /// 人が見つからないときは空文字
    /// </param>
    /// <returns>カードに出す名前</returns>
    /// <remarks>共有しないときは空文字で、読めなかったときは UnknownUserName。</remarks>
    private string NameOf(string name)
    {
        if (!repository.IsShared)
        {
            return "";
        }
        return name.Length > 0 ? name : UnknownUserName;
    }
}
