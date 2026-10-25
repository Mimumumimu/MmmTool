using Microsoft.Data.SqlClient;
using MmmTool.Data.CodeMaster;

namespace MmmTool.Data.SqlServer.CodeMaster;

/// <summary>
/// コードマスタの表 (<c>dbo.CodeMaster</c>)の行を、SQL Server から読む。
/// </summary>
/// <remarks>読み出しの列の並びと、読み方を 1 か所に置き、作業管理の読み込みと、コードマスタの編集が同じものを使う。</remarks>
internal static class CodeMasterRowReader
{
    /// <summary>読み出す列の並び (12 列)</summary>
    /// <param name="alias">表の別名</param>
    /// <returns>共通ヘッダー 6 列と、コードマスタの中身 6 列の並び</returns>
    public static string Columns(string alias)
        => $"{alias}.Id, {alias}.IsDeleted, {alias}.CreatedAt, {alias}.CreatedByUserId, {alias}.UpdatedAt, {alias}.UpdatedByUserId, "
            + $"{alias}.FeatureNo, {alias}.CategoryNo, {alias}.CodeNo, {alias}.SortOrder, {alias}.NumericValue, {alias}.Name";

    /// <summary>コードマスタの行を読む</summary>
    /// <param name="r">読み取り中の結果。<see cref="Columns"/> の並びで読み出したもの</param>
    /// <returns>行</returns>
    public static CodeMasterRow Read(SqlDataReader r) => new()
    {
        Id = r.GetInt32(0),
        IsDeleted = r.GetBoolean(1),
        CreatedAt = r.GetDateTimeOffset(2),
        CreatedByUserId = r.GetInt32(3),
        UpdatedAt = r.GetDateTimeOffset(4),
        UpdatedByUserId = r.GetInt32(5),
        FeatureNo = r.GetInt32(6),
        CategoryNo = r.GetInt32(7),
        CodeNo = r.GetInt32(8),
        SortOrder = r.GetInt32(9),
        NumericValue = r.GetInt32(10),
        Name = r.GetString(11),
    };
}
