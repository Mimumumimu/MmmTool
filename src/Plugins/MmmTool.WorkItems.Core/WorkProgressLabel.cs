namespace MmmTool.WorkItems.Core;

/// <summary>進捗度の値ごとのラベル (マスターの 1 行)</summary>
public sealed record WorkProgressLabel
{
    /// <summary>進捗度の値 (0 から 100 の 10 刻み)</summary>
    public int Value { get; init; }

    /// <summary>ラベル (括弧の中身。無ければ空)</summary>
    public string Label { get; init; } = "";

    /// <summary>更新日時 (保存先の時計。差分の取得の突き合わせに使う。保存するときは無視する)</summary>
    public DateTimeOffset UpdatedAt { get; init; }
}
