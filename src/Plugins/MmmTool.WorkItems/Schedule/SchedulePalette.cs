using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace MmmTool.WorkItems.Schedule;

/// <summary>スケジュールの色 (ライト・ダークの両方の値を持つ、名前のついた色)</summary>
/// <remarks>
/// 棒・休みの日の背景・今日の線のように、画面の部品が、コードで描くものの色。テーマごとに値を持たせ、どちらかを標準任せにしない。
/// 実績の棒の中の数字は、棒の背景に対して 4.5 倍以上のコントラストにする (ライトは薄い背景と濃い文字、ダークは明るい背景と暗い文字)。
/// 休みの日の背景は、土曜を薄い青、日曜と祝日を薄い赤にし、どちらも半透明の色にする (行の背景が、白の作業の行でも、青い案件・グループの行でも、約 1.2 倍以上の差で見える。中立な灰色にしない。灰色は、入力できない行のように見えるため)。
/// </remarks>
public sealed class SchedulePalette
{
    /// <summary>ライトの色</summary>
    private static readonly Lazy<SchedulePalette> LightPalette = new(() => new SchedulePalette(
        saturdayFill: Color.FromArgb(0x24, 0x1E, 0x5A, 0xC8),
        sundayFill: Color.FromArgb(0x24, 0xC4, 0x2B, 0x1C),
        line: Color.FromArgb(0xFF, 0xE6, 0xE8, 0xEE),
        today: Color.FromArgb(0xFF, 0xBE, 0x28, 0xA0),
        plan: Color.FromArgb(0xFF, 0xC4, 0xCE, 0xE2),
        planDone: Color.FromArgb(0xFF, 0x6E, 0x96, 0xE6),
        groupPlan: Color.FromArgb(0xFF, 0xAA, 0xBE, 0xE2),
        groupDone: Color.FromArgb(0xFF, 0x5A, 0x82, 0xD2),
        actual: Color.FromArgb(0xFF, 0xBC, 0xE8, 0xCE),
        actualEdge: Color.FromArgb(0xFF, 0x16, 0x7D, 0x46),
        actualSeparator: Color.FromArgb(0xFF, 0x78, 0xB9, 0x96),
        actualText: Color.FromArgb(0xFF, 0x04, 0x3A, 0x1E),
        critical: Color.FromArgb(0xFF, 0xC4, 0x2B, 0x1C),
        saturday: Color.FromArgb(0xFF, 0x1E, 0x5A, 0xC8),
        sunday: Color.FromArgb(0xFF, 0xC4, 0x2B, 0x1C)));

    /// <summary>ダークの色</summary>
    private static readonly Lazy<SchedulePalette> DarkPalette = new(() => new SchedulePalette(
        saturdayFill: Color.FromArgb(0x24, 0x78, 0xAA, 0xFF),
        sundayFill: Color.FromArgb(0x24, 0xFF, 0x99, 0xA4),
        line: Color.FromArgb(0xFF, 0x32, 0x34, 0x3A),
        today: Color.FromArgb(0xFF, 0xDC, 0x5A, 0xC8),
        plan: Color.FromArgb(0xFF, 0x42, 0x50, 0x6C),
        planDone: Color.FromArgb(0xFF, 0x62, 0x8C, 0xE2),
        groupPlan: Color.FromArgb(0xFF, 0x34, 0x4A, 0x78),
        groupDone: Color.FromArgb(0xFF, 0x4E, 0x76, 0xC8),
        actual: Color.FromArgb(0xFF, 0x46, 0xC3, 0x7D),
        actualEdge: Color.FromArgb(0xFF, 0x1E, 0x5A, 0x3C),
        actualSeparator: Color.FromArgb(0xFF, 0x28, 0x82, 0x50),
        actualText: Color.FromArgb(0xFF, 0x08, 0x1E, 0x12),
        critical: Color.FromArgb(0xFF, 0xFF, 0x99, 0xA4),
        saturday: Color.FromArgb(0xFF, 0x78, 0xAA, 0xFF),
        sunday: Color.FromArgb(0xFF, 0xFF, 0x99, 0xA4)));

    /// <summary>テーマに合った色を返す</summary>
    /// <param name="theme">要素の実際のテーマ (<see cref="FrameworkElement.ActualTheme"/>)</param>
    /// <returns>ダークならダークの色、それ以外はライトの色</returns>
    public static SchedulePalette For(ElementTheme theme) => theme == ElementTheme.Dark ? DarkPalette.Value : LightPalette.Value;

    /// <summary>色の組を作る</summary>
    private SchedulePalette(Color saturdayFill, Color sundayFill, Color line, Color today, Color plan, Color planDone, Color groupPlan, Color groupDone, Color actual, Color actualEdge, Color actualSeparator, Color actualText, Color critical, Color saturday, Color sunday)
    {
        SaturdayFill = new SolidColorBrush(saturdayFill);
        SundayFill = new SolidColorBrush(sundayFill);
        Line = new SolidColorBrush(line);
        Today = new SolidColorBrush(today);
        Plan = new SolidColorBrush(plan);
        PlanDone = new SolidColorBrush(planDone);
        GroupPlan = new SolidColorBrush(groupPlan);
        GroupDone = new SolidColorBrush(groupDone);
        Actual = new SolidColorBrush(actual);
        ActualEdge = new SolidColorBrush(actualEdge);
        ActualSeparator = new SolidColorBrush(actualSeparator);
        ActualText = new SolidColorBrush(actualText);
        Critical = new SolidColorBrush(critical);
        Saturday = new SolidColorBrush(saturday);
        Sunday = new SolidColorBrush(sunday);
    }

    /// <summary>土曜の列の背景 (薄い青)</summary>
    public SolidColorBrush SaturdayFill { get; }

    /// <summary>日曜・祝日の列の背景 (薄い赤)</summary>
    public SolidColorBrush SundayFill { get; }

    /// <summary>日付の区切りの線</summary>
    public SolidColorBrush Line { get; }

    /// <summary>今日の線と、見出しの今日の印</summary>
    public SolidColorBrush Today { get; }

    /// <summary>作業の予定の棒 (進捗度で塗らない部分)</summary>
    public SolidColorBrush Plan { get; }

    /// <summary>作業の予定の棒の、進捗度で塗る部分</summary>
    public SolidColorBrush PlanDone { get; }

    /// <summary>案件・グループの予定の棒 (進捗度で塗らない部分)</summary>
    public SolidColorBrush GroupPlan { get; }

    /// <summary>案件・グループの予定の棒の、進捗度で塗る部分</summary>
    public SolidColorBrush GroupDone { get; }

    /// <summary>実績の棒の背景</summary>
    public SolidColorBrush Actual { get; }

    /// <summary>実績の棒の枠</summary>
    public SolidColorBrush ActualEdge { get; }

    /// <summary>実績の棒の中の、日の境目の線</summary>
    public SolidColorBrush ActualSeparator { get; }

    /// <summary>実績の棒の中の、時間の数字</summary>
    public SolidColorBrush ActualText { get; }

    /// <summary>期限切れの枠</summary>
    public SolidColorBrush Critical { get; }

    /// <summary>見出しの、土曜の文字</summary>
    public SolidColorBrush Saturday { get; }

    /// <summary>見出しの、日曜・祝日の文字</summary>
    public SolidColorBrush Sunday { get; }
}
