using Windows.UI;

namespace MmmTool.WorkItems;

/// <summary>作業管理の表とスケジュールの表で共通の、行に重ねる色</summary>
/// <remarks>どちらの表も、同じ意味の行に同じ色を重ねる。色の値を 1 か所だけで持つ。</remarks>
internal static class WorkRowTints
{
    /// <summary>案件の行の色 (青の半透明)</summary>
    public static readonly Color Project = Color.FromArgb(0x2E, 0x4D, 0x8F, 0xE0);

    /// <summary>完了した作業の行の色 (灰色の半透明)</summary>
    /// <remarks>中間の灰色 (#808080)の約 22%。ライトでは白い背景が少し暗く、ダークでは暗い背景が少し明るくなり、どちらも背景に対して約 1.3 倍の差で、終わった行と分かる。</remarks>
    public static readonly Color Finished = Color.FromArgb(0x38, 0x80, 0x80, 0x80);
}
