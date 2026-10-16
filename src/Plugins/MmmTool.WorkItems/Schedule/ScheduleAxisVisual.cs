using Microsoft.UI.Xaml.Media;
using MmmSdk.Core.Components.Holidays;
using MmmTool.WorkItems.Core;

namespace MmmTool.WorkItems.Schedule;

/// <summary>日付の軸と、行ごとのセルが使う、休みの日の背景・日付の線の位置</summary>
/// <param name="axis">日付の軸</param>
/// <remarks>
/// 位置 (長方形・線の座標)は、軸ごとに 1 回だけ数えて持ち、すべての行のセルが使う (行を使い回すたびに、日付を数え直さないため)。
/// 図形 (<see cref="Geometry"/>)は、セルごとに作る (1 つの図形を、複数の <c>Path</c> に付けられないため)。
/// 軸の範囲・単位が変わったときだけ、新しく作る。
/// 休みの日の背景は、土曜 (薄い青)と、日曜・祝日 (薄い赤)で色を分ける。祝日が土曜に重なるときは、祝日 (赤)にする。
/// </remarks>
public sealed class ScheduleAxisVisual(ScheduleAxis axis)
{
    /// <summary>行の高さ (表の行の高さと同じ。図形の高さに使う)</summary>
    public const double RowHeight = 36;

    /// <summary>土曜の背景の位置 (遅延して数える)</summary>
    private IReadOnlyList<Windows.Foundation.Rect>? _saturdayRects;

    /// <summary>日曜・祝日の背景の位置 (遅延して数える)</summary>
    private IReadOnlyList<Windows.Foundation.Rect>? _sundayRects;

    /// <summary>日付の線の左右の位置 (遅延して数える)</summary>
    private IReadOnlyList<double>? _lineXs;

    /// <summary>日付の軸</summary>
    public ScheduleAxis Axis { get; } = axis;

    /// <summary>土曜の背景の図形を作る (連続した日は 1 つの長方形にまとめる。月の単位は空)</summary>
    /// <returns>新しい図形 (呼ぶ側の <c>Path</c> が持つ)</returns>
    public Geometry CreateSaturdays() => Rectangles(_saturdayRects ?? CountOffDays().Saturdays);

    /// <summary>日曜・祝日の背景の図形を作る (連続した日は 1 つの長方形にまとめる。月の単位は空)</summary>
    /// <returns>新しい図形 (呼ぶ側の <c>Path</c> が持つ)</returns>
    public Geometry CreateSundays() => Rectangles(_sundayRects ?? CountOffDays().Sundays);

    /// <summary>日付の区切りの線の図形を作る (日は 1 日ごと、週は月曜の前、月は月の初めの前)</summary>
    /// <returns>新しい図形 (呼ぶ側の <c>Path</c> が持つ)</returns>
    public Geometry CreateLines()
    {
        var group = new GeometryGroup();
        foreach (var x in _lineXs ??= CountLines())
        {
            group.Children.Add(new LineGeometry { StartPoint = new Windows.Foundation.Point(x, 0), EndPoint = new Windows.Foundation.Point(x, RowHeight) });
        }
        return group;
    }

    /// <summary>長方形の並びから、図形を作る</summary>
    /// <param name="rects">長方形の並び</param>
    /// <returns>新しい図形</returns>
    private static GeometryGroup Rectangles(IReadOnlyList<Windows.Foundation.Rect> rects)
    {
        var group = new GeometryGroup();
        foreach (var rect in rects)
        {
            group.Children.Add(new RectangleGeometry { Rect = rect });
        }
        return group;
    }

    /// <summary>休みの日の背景の位置を、土曜と、日曜・祝日に分けて数える (結果は持ち、次からは数え直さない)</summary>
    /// <returns>土曜の長方形の並びと、日曜・祝日の長方形の並び (連続した同じ種類の日を 1 つにまとめたもの。月の単位は、どちらも空)</returns>
    private (IReadOnlyList<Windows.Foundation.Rect> Saturdays, IReadOnlyList<Windows.Foundation.Rect> Sundays) CountOffDays()
    {
        var saturdays = new List<Windows.Foundation.Rect>();
        var sundays = new List<Windows.Foundation.Rect>();
        if (Axis.ShowsOffDays)
        {
            DateOnly runStart = default;
            var runDays = 0;
            var runIsSunday = false;
            void Flush()
            {
                if (runDays > 0)
                {
                    (runIsSunday ? sundays : saturdays).Add(new Windows.Foundation.Rect(Axis.X(runStart), 0, runDays * Axis.DayWidth, RowHeight));
                    runDays = 0;
                }
            }

            for (var date = Axis.Start; date <= Axis.End; date = date.AddDays(1))
            {
                var isSunday = date.DayOfWeek == DayOfWeek.Sunday || JapaneseHolidays.IsHoliday(date);
                var isSaturday = !isSunday && date.DayOfWeek == DayOfWeek.Saturday;
                if (!isSunday && !isSaturday)
                {
                    Flush();
                }
                else if (runDays > 0 && runIsSunday == isSunday)
                {
                    runDays++;
                }
                else
                {
                    Flush();
                    runStart = date;
                    runDays = 1;
                    runIsSunday = isSunday;
                }
            }
            Flush();
        }

        _saturdayRects = saturdays;
        _sundayRects = sundays;
        return (saturdays, sundays);
    }

    /// <summary>日付の線の位置を数える</summary>
    /// <returns>線の左右の位置の並び</returns>
    private List<double> CountLines()
    {
        var xs = new List<double>();
        for (var date = Axis.Start.AddDays(1); date <= Axis.End; date = date.AddDays(1))
        {
            var isBoundary = Axis.Unit switch
            {
                ScheduleUnit.Day => true,
                ScheduleUnit.Week => date.DayOfWeek == DayOfWeek.Monday,
                _ => date.Day == 1,
            };
            if (isBoundary)
            {
                xs.Add(Axis.X(date));
            }
        }
        return xs;
    }
}
