using MmmTool.CodeMaster.Core;

namespace MmmTool.Data.CodeMaster;

/// <summary>
/// DB の <c>CodeMaster</c> (コードマスタ)の 1 行 (列と 1 対 1)。
/// </summary>
/// <remarks>(<see cref="FeatureNo"/>, <see cref="CategoryNo"/>, <see cref="CodeNo"/>) は一意。名前の「ない」は空文字、数値の「ない」は 0 (NULL は使わない)。</remarks>
public sealed record CodeMasterRow
{
    /// <summary>主キー。0 は新規 (DB が採番する)</summary>
    public int Id { get; init; }

    /// <summary>論理削除されているか</summary>
    public bool IsDeleted { get; init; }

    /// <summary>作成日時</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>作成者 (ユーザーの番号。0 は特定のユーザーではない)</summary>
    public int CreatedByUserId { get; init; }

    /// <summary>更新日時</summary>
    public DateTimeOffset UpdatedAt { get; init; }

    /// <summary>更新者 (ユーザーの番号。0 は特定のユーザーではない)</summary>
    public int UpdatedByUserId { get; init; }

    /// <summary>機能番号</summary>
    public int FeatureNo { get; init; }

    /// <summary>区分番号</summary>
    public int CategoryNo { get; init; }

    /// <summary>コード番号</summary>
    public int CodeNo { get; init; }

    /// <summary>表示順</summary>
    public int SortOrder { get; init; }

    /// <summary>数値 (要らないコードは 0)</summary>
    public int NumericValue { get; init; }

    /// <summary>名前 (無いときは空文字)</summary>
    public string Name { get; init; } = "";

    /// <summary>DB の行から、アプリの型にする</summary>
    /// <returns>コードマスタの 1 件</returns>
    public CodeMasterEntry ToEntry() => new()
    {
        FeatureNo = FeatureNo,
        CategoryNo = CategoryNo,
        CodeNo = CodeNo,
        SortOrder = SortOrder,
        NumericValue = NumericValue,
        Name = Name,
        IsDeleted = IsDeleted,
        UpdatedAt = UpdatedAt,
    };
}
