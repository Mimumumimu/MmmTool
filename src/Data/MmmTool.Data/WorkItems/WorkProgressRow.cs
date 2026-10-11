using MmmTool.WorkItems.Core;

namespace MmmTool.Data.WorkItems;

/// <summary>
/// DB の <c>WorkProgress</c> (進捗度のマスター)の 1 行 (列と 1 対 1)。
/// </summary>
/// <remarks>値は 0 から 100 の 10 刻みで固定。マスターが持つのは、値ごとのラベルを付けるか付けないかだけ。</remarks>
public sealed record WorkProgressRow
{
    /// <summary>主キー。0 は新規 (DB が採番する)</summary>
    public int Id { get; init; }

    /// <summary>論理削除されているか</summary>
    public bool IsDeleted { get; init; }

    /// <summary>作成日時</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>作成者 (ユーザーの番号)</summary>
    public int CreatedByUserId { get; init; }

    /// <summary>更新日時</summary>
    public DateTimeOffset UpdatedAt { get; init; }

    /// <summary>更新者 (ユーザーの番号)</summary>
    public int UpdatedByUserId { get; init; }

    /// <summary>進捗度の値 (0 から 100 の 10 刻み)</summary>
    public byte Value { get; init; }

    /// <summary>ラベル (括弧の中身。無いときは空文字)</summary>
    public string Label { get; init; } = "";

    /// <summary>アプリの型から、DB の行にする</summary>
    /// <param name="label">進捗度のラベル</param>
    /// <returns>DB の行。作成・更新の日時と作成者・更新者は決めない (Repository が入れる)</returns>
    /// <exception cref="ArgumentException">値が 10 刻みでない・ラベルが長すぎる。</exception>
    public static WorkProgressRow FromWorkProgressLabel(WorkProgressLabel label)
    {
        ArgumentNullException.ThrowIfNull(label);
        if (!WorkProgress.IsValid(label.Value))
        {
            throw new ArgumentException($"進捗度の値 {label.Value} は、0 から 100 の 10 刻みではありません。", nameof(label));
        }
        return new WorkProgressRow
        {
            Value = (byte)label.Value,
            Label = WorkRowValues.CheckText(label.Label, "ラベル", WorkProgress.LabelMaxLength),
        };
    }

    /// <summary>DB の行から、アプリの型にする</summary>
    /// <returns>進捗度のラベル</returns>
    public WorkProgressLabel ToWorkProgressLabel() => new() { Value = Value, Label = Label, UpdatedAt = UpdatedAt };
}
