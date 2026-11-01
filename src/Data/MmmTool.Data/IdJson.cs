namespace MmmTool.Data;

/// <summary>
/// 番号の一覧を、問い合わせの 1 つのパラメーター (JSON の配列)にする。
/// </summary>
/// <remarks>
/// 番号をパラメーターに 1 つずつ並べると、SQL Server は数に上限 (2100)があり、問い合わせの形も変わる。
/// 1 つの JSON なら、何件でも同じ問い合わせになる。SQL Server は <c>OPENJSON</c>、SQLite は <c>json_each</c> で行に戻す。
/// 保存先ごとに別々に作らないため、ここに 1 つだけ置く。
/// </remarks>
public static class IdJson
{
    /// <summary>番号の一覧を、JSON の配列にする</summary>
    /// <param name="ids">番号</param>
    /// <returns>JSON の配列で、例は <c>[1,5,7]</c>。番号は整数だけなので、文字の組み立てで足りる</returns>
    public static string From(IEnumerable<int> ids)
    {
        ArgumentNullException.ThrowIfNull(ids);
        return "[" + string.Join(',', ids) + "]";
    }
}
