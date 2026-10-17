using MmmTool.Users.Core;

namespace MmmTool.Notes.Core;

/// <summary>
/// ノートの読み書きの入口。並び・名前の引き方・今日の更新の印を決める。
/// </summary>
/// <param name="repository">ノートの保存先</param>
/// <param name="users">ユーザーの保存先 (作成者・更新者の名前を引く。DB モードだけで使う)</param>
/// <param name="time">現在時刻の取得元</param>
/// <remarks>
/// 並びは、重要度の高い順、同じ重要度の中は更新日時の新しい順 (同じ更新日時なら番号の大きい順)。
/// 作成者・更新者の名前は、SQL で結合せず、必要な人だけを番号で引く (<see cref="IAppUserRepository.GetUsersByIdsAsync"/>)。
/// </remarks>
public sealed class NoteService(INoteRepository repository, IAppUserRepository users, TimeProvider time)
{
    /// <summary>名前が引けなかったユーザーの表示</summary>
    public const string UnknownUserName = "(不明なユーザー)";

    /// <summary>複数の人で共有するか (DB モード)</summary>
    public bool IsShared => repository.IsShared;

    /// <summary>今のユーザーの番号 (ローカルモードは 1)</summary>
    /// <exception cref="InvalidOperationException">DB モードで、今のユーザーをまだ特定していない。</exception>
    public int CurrentUserId => repository.CurrentUserId;

    /// <summary>実際の今日 (この PC の日付)</summary>
    public DateOnly Today => DateOnly.FromDateTime(time.GetLocalNow().DateTime);

    /// <summary>指定の日に有効なノートを、並べて取得する</summary>
    /// <param name="day">日</param>
    /// <param name="includeDeleted">削除済みも含めるか</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>カードに出す内容 (重要度の高い順、同じ重要度の中は更新日時の新しい順)</returns>
    /// <exception cref="MmmSdk.Core.Components.Storage.DataFileException">読めなかった (メッセージは画面に出せる)。</exception>
    public async Task<IReadOnlyList<NoteCard>> LoadAsync(DateOnly day, bool includeDeleted, CancellationToken cancellationToken = default)
    {
        var notes = await repository.GetOnDayAsync(day, includeDeleted, cancellationToken).ConfigureAwait(false);
        var sorted = notes
            .OrderByDescending(note => note.Priority)
            .ThenByDescending(note => note.UpdatedAt)
            .ThenByDescending(note => note.Id)
            .ToList();

        var names = await LoadNamesAsync(sorted, cancellationToken).ConfigureAwait(false);
        var today = Today;
        return [.. sorted.Select(note => new NoteCard(
            note,
            NameOf(names, note.AuthorUserId),
            NameOf(names, note.UpdatedByUserId),
            DateOnly.FromDateTime(note.UpdatedAt.ToLocalTime().DateTime) == today))];
    }

    /// <summary>ノートを追加する</summary>
    /// <param name="input">入力内容</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>保存した内容</returns>
    /// <exception cref="ArgumentException">入力内容が正しくない (<see cref="NoteInput.Validate"/> で先に調べる)。</exception>
    /// <exception cref="MmmSdk.Core.Components.Storage.DataFileException">保存できなかった (メッセージは画面に出せる)。</exception>
    public Task<Note> AddAsync(NoteInput input, CancellationToken cancellationToken = default)
        => repository.AddAsync(Checked(input), cancellationToken);

    /// <summary>ノートを変更する</summary>
    /// <param name="id">ノートの番号</param>
    /// <param name="input">入力内容</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>変更したら true。見えない・存在しないときは false</returns>
    /// <exception cref="ArgumentException">入力内容が正しくない。</exception>
    /// <exception cref="MmmSdk.Core.Components.Storage.DataFileException">保存できなかった (メッセージは画面に出せる)。</exception>
    public Task<bool> UpdateAsync(int id, NoteInput input, CancellationToken cancellationToken = default)
        => repository.UpdateAsync(id, Checked(input), cancellationToken);

    /// <summary>ノートを削除する・復元する (論理削除)</summary>
    /// <param name="id">ノートの番号</param>
    /// <param name="isDeleted">削除するなら true、復元するなら false</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>変更したら true。見えない・存在しないときは false</returns>
    /// <exception cref="MmmSdk.Core.Components.Storage.DataFileException">保存できなかった (メッセージは画面に出せる)。</exception>
    public Task<bool> SetDeletedAsync(int id, bool isDeleted, CancellationToken cancellationToken = default)
        => repository.SetDeletedAsync(id, isDeleted, cancellationToken);

    /// <summary>入力内容を調べて、前後の空白を取り除く</summary>
    /// <param name="input">入力内容</param>
    /// <returns>保存する内容</returns>
    /// <exception cref="ArgumentException">入力内容が正しくない。</exception>
    private static NoteInput Checked(NoteInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.Validate() is { } error)
        {
            throw new ArgumentException(error, nameof(input));
        }
        return input.Trimmed();
    }

    /// <summary>カードに出す作成者・更新者の名前を引く</summary>
    /// <param name="notes">ノート</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>ユーザーの番号と表示名 (共有しないとき (ローカルモード)は空)</returns>
    private async Task<Dictionary<int, string>> LoadNamesAsync(List<Note> notes, CancellationToken cancellationToken)
    {
        if (!repository.IsShared || notes.Count == 0)
        {
            return [];
        }

        var ids = notes.SelectMany(note => new[] { note.AuthorUserId, note.UpdatedByUserId }).Distinct().ToArray();
        var found = await users.GetUsersByIdsAsync(ids, cancellationToken).ConfigureAwait(false);
        return found.ToDictionary(user => user.Id, user => user.DisplayName);
    }

    /// <summary>名前を引く (共有しないときは空文字。引けなかったときは <see cref="UnknownUserName"/>)</summary>
    /// <param name="names">ユーザーの番号と表示名</param>
    /// <param name="userId">ユーザーの番号</param>
    /// <returns>名前</returns>
    private string NameOf(Dictionary<int, string> names, int userId)
    {
        if (!repository.IsShared)
        {
            return "";
        }
        return names.TryGetValue(userId, out var name) && name.Length > 0 ? name : UnknownUserName;
    }
}
