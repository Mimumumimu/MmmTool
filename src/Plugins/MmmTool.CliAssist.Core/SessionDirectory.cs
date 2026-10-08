namespace MmmTool.CliAssist.Core;

/// <summary>セッション (タブ)の作業フォルダの比較と、タブ名の作成</summary>
/// <remarks>
/// パスは文字列のまま扱い、ファイルシステムには触れない (入力のたびに呼んでも止まらず、不正な文字があっても例外にならないため)。
/// 区切りは <c>\</c> と <c>/</c> のどちらも認め、大文字小文字は区別しない (Windows のパス)。
/// </remarks>
public static class SessionDirectory
{
    /// <summary>2 つのフォルダが同じか</summary>
    /// <param name="first">1 つ目のパス</param>
    /// <param name="second">2 つ目のパス</param>
    /// <returns>同じフォルダを指すなら true (区切りの種類・末尾の区切り・大文字小文字の違いは無視する)</returns>
    public static bool IsSame(string first, string second)
        => string.Equals(Normalize(first), Normalize(second), StringComparison.OrdinalIgnoreCase);

    /// <summary>タブ名を作る</summary>
    /// <param name="directories">タブごとの作業フォルダ (タブの並び順)</param>
    /// <returns>タブごとのタブ名 (入力と同じ並び順)</returns>
    /// <remarks>
    /// タブ名はフォルダ名だけにする。フォルダ名が同じで場所が違うタブがあるときだけ、親フォルダ名を足す (<c>名前 (親)</c>)。
    /// それでも同じになるときは、パス全体にする。
    /// </remarks>
    public static IReadOnlyList<string> CreateTitles(IReadOnlyList<string> directories)
    {
        var segments = directories.Select(Split).ToList();
        var titles = segments
            .Select((parts, index) => parts.Length > 0 ? parts[^1] : Normalize(directories[index]))
            .ToList();

        foreach (var group in DuplicateGroups(titles))
        {
            foreach (var index in group)
            {
                var parts = segments[index];
                if (parts.Length >= 2)
                {
                    titles[index] = $"{titles[index]} ({parts[^2]})";
                }
            }
        }

        foreach (var group in DuplicateGroups(titles))
        {
            foreach (var index in group)
            {
                titles[index] = Normalize(directories[index]);
            }
        }
        return titles;
    }

    /// <summary>同じタブ名を持つタブの添字を、グループにして返す (2 つ以上あるものだけ)</summary>
    /// <param name="titles">タブ名の一覧</param>
    /// <returns>同じタブ名 (大文字小文字は区別しない)ごとの、添字の一覧</returns>
    private static IEnumerable<int[]> DuplicateGroups(List<string> titles)
        => titles
            .Select((title, index) => (title, index))
            .GroupBy(item => item.title, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Select(item => item.index).ToArray())
            .ToList();

    /// <summary>区切りを <c>\</c> にそろえ、前後の空白と末尾の区切りを取り除く</summary>
    /// <param name="path">パス</param>
    /// <returns>比べやすい形にしたパス</returns>
    private static string Normalize(string path)
        => path.Trim().Replace('/', '\\').TrimEnd('\\');

    /// <summary>パスを、フォルダ名の並びに分ける</summary>
    /// <param name="path">パス</param>
    /// <returns>フォルダ名の並び (<c>C:\work\app</c> なら <c>C:</c> <c>work</c> <c>app</c>)。空のパスは空</returns>
    private static string[] Split(string path)
        => Normalize(path).Split('\\', StringSplitOptions.RemoveEmptyEntries);
}
