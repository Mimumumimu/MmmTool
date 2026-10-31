using System.Text.Json;
using MmmTool.WorkItems.Core;

namespace MmmTool.Data.WorkItems;

/// <summary>
/// 行の位置 (<see cref="WorkPosition"/>)の一覧を、問い合わせの 1 つのパラメーター (JSON の配列)にする。
/// </summary>
/// <remarks>
/// 位置の数だけ <c>UPDATE</c> を流さず、1 つの <c>UPDATE</c> にこの JSON を渡して、まとめて書くために使う。
/// 要素は <c>{"id":行の番号,"project":案件,"parent":親,"order":並び}</c>。SQL Server は <c>OPENJSON</c>、SQLite は <c>json_each</c> で行に戻す。
/// </remarks>
public static class PositionJson
{
    /// <summary>位置の一覧を、JSON の配列にする</summary>
    /// <param name="positions">位置</param>
    /// <returns>JSON の配列</returns>
    public static string From(IEnumerable<WorkPosition> positions)
    {
        ArgumentNullException.ThrowIfNull(positions);
        return JsonSerializer.Serialize(positions.Select(position => new { id = position.Id, project = position.ProjectId, parent = position.ParentId, order = position.SortOrder }));
    }
}
