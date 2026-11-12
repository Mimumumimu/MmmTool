namespace MmmTool.CodeMaster.Core;

/// <summary>コードマスタの 1 件で、保存するデータ</summary>
/// <remarks>
/// 保存先に依存しない。
/// (<see cref="FeatureNo"/>, <see cref="CategoryNo"/>, <see cref="CodeNo"/>) で 1 件が決まる。
/// 利用する側の行は <see cref="CodeNo"/> を保存する。
/// <see cref="NumericValue"/> は計算に使う数 (進捗度の % など)で、要らないコードは 0。
/// </remarks>
public sealed record CodeMasterEntry
{
    /// <summary>名前の最大の長さ</summary>
    public const int NameMaxLength = 100;

    /// <summary>機能番号</summary>
    /// <remarks><see cref="CodeFeatures"/></remarks>
    public int FeatureNo { get; init; }

    /// <summary>区分番号で、機能の中のコードの種類</summary>
    /// <remarks>利用する機能が決める</remarks>
    public int CategoryNo { get; init; }

    /// <summary>コード番号で、区分の中の識別子</summary>
    /// <remarks>一度使った番号は同じ意味のまま変えない</remarks>
    public int CodeNo { get; init; }

    /// <summary>表示順</summary>
    public int SortOrder { get; init; }

    /// <summary>数値で、計算に使う数</summary>
    /// <remarks>要らないコードは 0</remarks>
    public int NumericValue { get; init; }

    /// <summary>名前</summary>
    /// <remarks>空でもよい</remarks>
    public string Name { get; init; } = "";

    /// <summary>論理削除されているか</summary>
    /// <remarks>選択肢から外れていて、過去の行が持つコードの表示には使える。</remarks>
    public bool IsDeleted { get; init; }

    /// <summary>更新日時で、保存先の時計</summary>
    /// <remarks>
    /// 差分の取得の突き合わせに使う
    /// 保存するときは無視する
    /// </remarks>
    public DateTimeOffset UpdatedAt { get; init; }

    /// <summary>同じ種類・同じコードの 1 件を指すキー</summary>
    public (int FeatureNo, int CategoryNo, int CodeNo) Key => (FeatureNo, CategoryNo, CodeNo);
}
