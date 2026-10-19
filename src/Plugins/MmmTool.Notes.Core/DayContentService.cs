using MmmTool.DayContent.Core;
using MmmTool.Users.Core;

namespace MmmTool.Notes.Core;

/// <summary>
/// 登録された他の機能の内容 (<see cref="IDayContentSource"/>)を集めて、ノートのカードの上に出す枠にする。
/// </summary>
/// <param name="sources">登録された内容の提供元</param>
/// <param name="users">ユーザーの保存先 (全員のとき、枠の見出しに出す名前を引く)</param>
/// <remarks>
/// 提供する機能の名前を持たない。「全員」のときは、人ごとに枠を作り、自分の枠を先頭にして、あとは名前順に並べる。
/// 名前は、提供元が返した番号の人だけを <see cref="IAppUserRepository.GetUsersByIdsAsync"/> で引く。
/// </remarks>
public sealed class DayContentService(IEnumerable<IDayContentSource> sources, IAppUserRepository users)
{
    /// <summary>自分の枠に出す、名前が引けないときの名前</summary>
    private const string SelfName = "自分";

    /// <summary>登録された内容の提供元</summary>
    public IReadOnlyList<IDayContentSource> Sources { get; } = [.. sources];

    /// <summary>提供元の内容を、枠にして取得する</summary>
    /// <param name="source">提供元</param>
    /// <param name="from">期間の開始日</param>
    /// <param name="to">期間の終了日 (ノートで表示している日)</param>
    /// <param name="everyone">全員の分を出すか (false は自分だけ)</param>
    /// <param name="selfUserId">自分のユーザーの番号 (全員のとき、自分の枠を先頭にする)</param>
    /// <param name="options">選択肢の値で、キーは <see cref="DayContentOption.Key"/></param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>枠。自分だけのときは 1 枠</returns>
    /// <exception cref="MmmSdk.Core.Components.Storage.DataFileException">読めなかった。メッセージは画面に出せる。</exception>
    public async Task<IReadOnlyList<DayContentFrame>> LoadAsync(
        IDayContentSource source, DateOnly from, DateOnly to, bool everyone, int selfUserId, IReadOnlyDictionary<string, bool> options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(options);

        if (!everyone)
        {
            return [new DayContentFrame(source.Title, await source.GetTextAsync(from, to, null, options, cancellationToken).ConfigureAwait(false))];
        }

        var others = (await source.GetPeopleAsync(from, to, cancellationToken).ConfigureAwait(false)).Where(id => id != selfUserId).Distinct().ToList();
        var found = await users.GetUsersByIdsAsync([selfUserId, .. others], cancellationToken).ConfigureAwait(false);
        var names = found.ToDictionary(user => user.Id, user => user.DisplayName);

        var ordered = new List<(int UserId, string Name)> { (selfUserId, names.GetValueOrDefault(selfUserId, SelfName)) };
        ordered.AddRange(others
            .Select(id => (UserId: id, Name: names.GetValueOrDefault(id, NoteService.UnknownUserName)))
            .OrderBy(person => person.Name, StringComparer.CurrentCulture)
            .ThenBy(person => person.UserId));

        var frames = new List<DayContentFrame>(ordered.Count);
        foreach (var person in ordered)
        {
            var text = await source.GetTextAsync(from, to, person.UserId, options, cancellationToken).ConfigureAwait(false);
            frames.Add(new DayContentFrame($"{source.Title} - {person.Name}", text));
        }
        return frames;
    }
}
