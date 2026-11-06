namespace MmmTool.WorkItems.Core;

/// <summary>案件で、案件マスターの 1 件。保存するデータで、保存先に依存しない</summary>
/// <remarks>全員で共有する。画面に出すのは、利用者が選んだ案件だけ。日付・進捗度・予定時間・実績は、下の作業から計算する。計算は <see cref="WorkItemTree"/> が行う。</remarks>
public sealed record WorkProject
{
    /// <summary>番号。保存先が決め、0 は、まだ保存していない</summary>
    public int Id { get; init; }

    /// <summary>名前 (必須)</summary>
    public string Name { get; init; } = "";

    /// <summary>リンク。URL・フォルダ・ファイルのパスで、無いときは空文字</summary>
    public string Link { get; init; } = "";

    /// <summary>備考</summary>
    public string Note { get; init; } = "";

    /// <summary>論理削除されているか</summary>
    public bool IsDeleted { get; init; }

    /// <summary>更新日時で、保存先の時計。差分の取得の突き合わせに使う</summary>
    public DateTimeOffset UpdatedAt { get; init; }
}
