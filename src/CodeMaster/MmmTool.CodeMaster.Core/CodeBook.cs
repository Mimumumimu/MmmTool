namespace MmmTool.CodeMaster.Core;

/// <summary>読み込んだコードマスタを、区分ごとに引ける形にしたもの</summary>
/// <remarks>画面の選択肢は <see cref="Options"/> (論理削除されていない件)。保存済みの行が持つコードの名前は <see cref="Find"/> (論理削除された件も引ける)。</remarks>
public sealed class CodeBook
{
    /// <summary>何も入っていないコードブック</summary>
    public static CodeBook Empty { get; } = new([]);

    /// <summary>キーごとの件</summary>
    private readonly Dictionary<(int FeatureNo, int CategoryNo, int CodeNo), CodeMasterEntry> _entries;

    /// <summary>コードブックを作る</summary>
    /// <param name="entries">コードマスタの件 (論理削除された件を含んでよい。同じキーがあれば、あとのものが勝つ)</param>
    public CodeBook(IEnumerable<CodeMasterEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        _entries = [];
        foreach (var entry in entries)
        {
            _entries[entry.Key] = entry;
        }
    }

    /// <summary>件の数 (論理削除された件を含む)</summary>
    public int Count => _entries.Count;

    /// <summary>すべての件</summary>
    public IReadOnlyCollection<CodeMasterEntry> Entries => _entries.Values;

    /// <summary>区分の選択肢を返す</summary>
    /// <param name="featureNo">機能番号</param>
    /// <param name="categoryNo">区分番号</param>
    /// <returns>論理削除されていない件。表示順、同じならコード番号の順</returns>
    public IReadOnlyList<CodeMasterEntry> Options(int featureNo, int categoryNo)
        => [.. _entries.Values
            .Where(e => e.FeatureNo == featureNo && e.CategoryNo == categoryNo && !e.IsDeleted)
            .OrderBy(e => e.SortOrder)
            .ThenBy(e => e.CodeNo)];

    /// <summary>コードの 1 件を引く</summary>
    /// <param name="featureNo">機能番号</param>
    /// <param name="categoryNo">区分番号</param>
    /// <param name="codeNo">コード番号</param>
    /// <returns>見つかった件 (論理削除された件も返す)。無ければ null</returns>
    public CodeMasterEntry? Find(int featureNo, int categoryNo, int codeNo)
        => _entries.GetValueOrDefault((featureNo, categoryNo, codeNo));

    /// <summary>差分を反映した、新しいコードブックを返す</summary>
    /// <param name="changes">更新された件 (論理削除された件を含む)</param>
    /// <returns>反映後のコードブック。変わらなければ自分自身</returns>
    public CodeBook With(IEnumerable<CodeMasterEntry> changes)
    {
        var list = changes.ToList();
        return list.Count == 0 ? this : new CodeBook(_entries.Values.Concat(list));
    }
}
