namespace MmmTool.WorkItems.Core;

/// <summary>行の識別 (種類 + 番号)</summary>
/// <param name="Kind">種類</param>
/// <param name="Id">その種類の表での番号 (種類ごとに別の連番なので、種類と組で識別する)</param>
public readonly record struct WorkKey(WorkRowKind Kind, int Id)
{
    /// <summary>設定ストアに残す文字にする</summary>
    /// <returns>種類の文字 (案件は <c>P</c>、グループは <c>G</c>、作業は <c>W</c>)に番号をつないだもの (例: <c>P3</c>)</returns>
    public override string ToString() => $"{Kind switch { WorkRowKind.Project => 'P', WorkRowKind.Group => 'G', _ => 'W' }}{Id}";

    /// <summary>設定ストアの文字から、識別を読む</summary>
    /// <param name="text">文字 (<see cref="ToString"/> の形)</param>
    /// <param name="key">読めた識別</param>
    /// <returns>読めたら true (番号だけの古い形・壊れた文字は false)</returns>
    public static bool TryParse(string text, out WorkKey key)
    {
        key = default;
        if (text.Length < 2 || !int.TryParse(text.AsSpan(1), out var id) || id <= 0)
        {
            return false;
        }

        var kind = text[0] switch
        {
            'P' => WorkRowKind.Project,
            'G' => WorkRowKind.Group,
            'W' => WorkRowKind.Work,
            _ => (WorkRowKind?)null,
        };
        if (kind is null)
        {
            return false;
        }

        key = new WorkKey(kind.Value, id);
        return true;
    }
}
