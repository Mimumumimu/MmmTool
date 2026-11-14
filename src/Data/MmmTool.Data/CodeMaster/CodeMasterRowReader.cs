using System.Data.Common;
using MmmTool.Data.Queries;

namespace MmmTool.Data.CodeMaster;

/// <summary>コードマスタの行の読み取り</summary>
/// <remarks>
/// 読み出しの列の並びと読み方を 1 か所に置く。
/// 作業管理の読み込みとコードマスタの編集が、SQLite と SQL Server のどちらでも同じものを使う。
/// </remarks>
public static class CodeMasterRowReader
{
    /// <summary>読み出す列の並び</summary>
    /// <param name="alias">表の別名</param>
    /// <returns>
    /// 共通ヘッダー 6 列と、コードマスタの中身 6 列の並び。
    /// </returns>
    public static string Columns(string alias)
        => $"{alias}.Id, {alias}.IsDeleted, {alias}.CreatedAt, {alias}.CreatedByUserId, {alias}.UpdatedAt, {alias}.UpdatedByUserId, "
            + $"{alias}.FeatureNo, {alias}.CategoryNo, {alias}.CodeNo, {alias}.SortOrder, {alias}.NumericValue, {alias}.Name";

    /// <summary>コードマスタの行を読む</summary>
    /// <param name="r">
    /// 読み取り中の結果
    /// <see cref="Columns"/> の並びで読み出したもの
    /// </param>
    /// <param name="dialect">その DB の値の読み方</param>
    /// <returns>行</returns>
    public static CodeMasterRow Read(DbDataReader r, SqlDialect dialect) => new()
    {
        Id = r.GetInt32(0),
        IsDeleted = dialect.ReadBool(r, 1),
        CreatedAt = dialect.ReadDateTime(r, 2),
        CreatedByUserId = r.GetInt32(3),
        UpdatedAt = dialect.ReadDateTime(r, 4),
        UpdatedByUserId = r.GetInt32(5),
        FeatureNo = r.GetInt32(6),
        CategoryNo = r.GetInt32(7),
        CodeNo = r.GetInt32(8),
        SortOrder = r.GetInt32(9),
        NumericValue = r.GetInt32(10),
        Name = r.GetString(11),
    };
}
