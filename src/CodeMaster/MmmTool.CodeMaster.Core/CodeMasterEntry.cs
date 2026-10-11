namespace MmmTool.CodeMaster.Core;

/// <summary>コードマスタの 1 件 (保存するデータ。保存先に依存しない)</summary>
/// <remarks>
/// (<see cref="FeatureNo"/>, <see cref="CategoryNo"/>, <see cref="CodeNo"/>) で 1 件が決まる。
/// 利用する側の行は、<see cref="CodeNo"/> を保存する。<see cref="NumericValue"/> は、計算に使う数 (進捗度の % など)で、要らないコードは 0。
/// </remarks>
public sealed record CodeMasterEntry
{
    /// <summary>名前の最大の長さ</summary>
    public const int NameMaxLength = 100;

    /// <summary>機能番号 (<see cref="CodeFeatures"/>)</summary>
    public int FeatureNo { get; init; }

    /// <summary>区分番号 (機能の中の、コードの種類。利用する機能が決める)</summary>
    public int CategoryNo { get; init; }

    /// <summary>コード番号 (区分の中の識別子。一度使った番号は、同じ意味のまま変えない)</summary>
    public int CodeNo { get; init; }

    /// <summary>表示順 (小さいほど先)</summary>
    public int SortOrder { get; init; }

    /// <summary>数値 (計算に使う数。要らないコードは 0)</summary>
    public int NumericValue { get; init; }

    /// <summary>名前 (空でもよい)</summary>
    public string Name { get; init; } = "";

    /// <summary>論理削除されているか (選択肢から外れている。過去の行が持つコードの表示には、使える)</summary>
    public bool IsDeleted { get; init; }

    /// <summary>更新日時 (保存先の時計。差分の取得の突き合わせに使う。保存するときは無視する)</summary>
    public DateTimeOffset UpdatedAt { get; init; }

    /// <summary>同じ種類・同じコードの 1 件を指すキー</summary>
    public (int FeatureNo, int CategoryNo, int CodeNo) Key => (FeatureNo, CategoryNo, CodeNo);
}
