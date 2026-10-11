namespace MmmTool.WorkItems.Core;

/// <summary>案件 (案件マスターの 1 件。保存するデータ。保存先に依存しない)</summary>
/// <remarks>全員で共有する。画面に出すのは、利用者が選んだ案件だけ。日付・進捗度・予定時間・実績は、下の作業から計算する (<see cref="WorkItemTree"/>)。</remarks>
public sealed record WorkProject
{
    /// <summary>番号 (保存先が決める。0 は、まだ保存していない)</summary>
    public int Id { get; init; }

    /// <summary>名前 (必須)</summary>
    public string Name { get; init; } = "";

    /// <summary>備考</summary>
    public string Note { get; init; } = "";

    /// <summary>論理削除されているか</summary>
    public bool IsDeleted { get; init; }

    /// <summary>更新日時 (保存先の時計。差分の取得の突き合わせに使う)</summary>
    public DateTimeOffset UpdatedAt { get; init; }
}
