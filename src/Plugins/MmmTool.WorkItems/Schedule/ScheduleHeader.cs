using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using MmmSdk.Core.Components.Holidays;
using MmmTool.WorkItems.Core;

namespace MmmTool.WorkItems.Schedule;

/// <summary>スケジュールの日付の見出し (上が月か年、下が日付と曜日か、月曜の日付か、月)を作る</summary>
/// <remarks>
/// 見出しは、軸ごとに 1 つ作る (行のセルと違い、行の使い回しの影響を受けない)。表の見出しの高さは表全体で 1 つなので、<see cref="Height"/> を表に渡す。
/// 文字の色は、既定の文字色を使い (テーマに追従)、土曜・日曜・祝日だけ <see cref="SchedulePalette"/> の色にする。
/// </remarks>
public static class ScheduleHeader
{
    /// <summary>見出しの高さ (月・日付・曜日の 3 行が収まる)</summary>
    public const double Height = 56;

    /// <summary>1 行の高さ</summary>
    private const double LineHeight = 18;

    /// <summary>文字の大きさ</summary>
    private const double FontSize = 12;

    /// <summary>見出しを作る</summary>
    /// <param name="visual">日付の軸</param>
    /// <param name="palette">色</param>
    /// <param name="today">今日 (日の単位だけ、今日の列に印を付ける)</param>
    /// <returns>見出しの部品 (軸の幅の Canvas)</returns>
    public static UIElement Build(ScheduleAxisVisual visual, SchedulePalette palette, DateOnly today)
    {
        var axis = visual.Axis;
        var canvas = new Canvas { Width = axis.Width, Height = Height };

        switch (axis.Unit)
        {
            case ScheduleUnit.Day:
                AddMonthLabels(canvas, axis, "yyyy/M月");
                for (var date = axis.Start; date <= axis.End; date = date.AddDays(1))
                {
                    AddDayLabel(canvas, axis, palette, date, date == today);
                }
                break;
            case ScheduleUnit.Week:
                AddMonthLabels(canvas, axis, "yyyy/M月");
                for (var date = axis.Start; date <= axis.End; date = date.AddDays(7))
                {
                    var label = Text(date.Day.ToString(), FontSize, FontWeights.Normal);
                    Canvas.SetLeft(label, axis.X(date) + 4);
                    Canvas.SetTop(label, LineHeight + 10);
                    canvas.Children.Add(label);
                }
                break;
            default:
                for (var date = axis.Start; date <= axis.End; date = date.AddMonths(1))
                {
                    if (date.Month == 1 || date == axis.Start)
                    {
                        var year = Text($"{date.Year}年", FontSize, FontWeights.Normal);
                        year.Opacity = 0.7;
                        Canvas.SetLeft(year, axis.X(date) + 4);
                        Canvas.SetTop(year, 6);
                        canvas.Children.Add(year);
                    }
                    var month = Text($"{date.Month}月", FontSize, FontWeights.Normal);
                    Canvas.SetLeft(month, axis.X(date) + 4);
                    Canvas.SetTop(month, LineHeight + 10);
                    canvas.Children.Add(month);
                }
                break;
        }

        // 区切りの線 (行と同じ位置)
        var lines = new Microsoft.UI.Xaml.Shapes.Path { Data = LinesOfHeader(visual), Stroke = palette.Line, StrokeThickness = 1 };
        canvas.Children.Insert(0, lines);
        return canvas;
    }

    /// <summary>上の段に、月の初めごとの月の文字を足す</summary>
    /// <param name="canvas">見出し</param>
    /// <param name="axis">日付の軸</param>
    /// <param name="format">月の文字の書式</param>
    private static void AddMonthLabels(Canvas canvas, ScheduleAxis axis, string format)
    {
        for (var date = axis.Start; date <= axis.End; date = date.AddDays(1))
        {
            if (date.Day != 1 && date != axis.Start)
            {
                continue;
            }

            var label = Text(date.ToString(format, System.Globalization.CultureInfo.InvariantCulture), FontSize, FontWeights.Normal);
            label.Opacity = 0.7;
            Canvas.SetLeft(label, axis.X(date) + 4);
            Canvas.SetTop(label, 4);
            canvas.Children.Add(label);
        }
    }

    /// <summary>日の単位の、1 日ぶんの見出し (日付と曜日。土曜は青、日曜と祝日は赤。どちらも日付と曜日の両方。今日は印を付ける)を足す</summary>
    /// <param name="canvas">見出し</param>
    /// <param name="axis">日付の軸</param>
    /// <param name="palette">色</param>
    /// <param name="date">日付</param>
    /// <param name="isToday">今日か</param>
    private static void AddDayLabel(Canvas canvas, ScheduleAxis axis, SchedulePalette palette, DateOnly date, bool isToday)
    {
        var x = axis.X(date);

        if (isToday)
        {
            var mark = new Microsoft.UI.Xaml.Shapes.Rectangle
            {
                Width = axis.DayWidth - 4,
                Height = (LineHeight * 2) + 2,
                RadiusX = 5,
                RadiusY = 5,
                Fill = palette.Today,
            };
            Canvas.SetLeft(mark, x + 2);
            Canvas.SetTop(mark, LineHeight + 1);
            canvas.Children.Add(mark);
        }

        var number = Text(date.Day.ToString(), FontSize, FontWeights.Normal, axis.DayWidth);
        var weekday = Text(JapaneseWeekday(date), FontSize, FontWeights.Normal, axis.DayWidth);
        if (isToday)
        {
            number.Foreground = palette.TodayText;
            weekday.Foreground = palette.TodayText;
        }
        else if (date.DayOfWeek == DayOfWeek.Sunday || JapaneseHolidays.IsHoliday(date))
        {
            // 日曜と祝日は、日付も曜日も赤。土曜は、曜日だけ青
            number.Foreground = palette.Sunday;
            weekday.Foreground = palette.Sunday;
        }
        else if (date.DayOfWeek == DayOfWeek.Saturday)
        {
            // 土曜も、日付と曜日の両方を青 (日曜・祝日の赤と、そろえる)
            number.Foreground = palette.Saturday;
            weekday.Foreground = palette.Saturday;
        }
        Canvas.SetLeft(number, x);
        Canvas.SetTop(number, LineHeight + 2);
        Canvas.SetLeft(weekday, x);
        Canvas.SetTop(weekday, (LineHeight * 2) + 2);
        canvas.Children.Add(number);
        canvas.Children.Add(weekday);
    }

    /// <summary>見出しの高さの区切りの線 (行の線と同じ位置。行の線は高さが行の高さなので、見出しの高さに伸ばす)</summary>
    /// <param name="visual">日付の軸</param>
    /// <returns>線の図形</returns>
    private static Geometry LinesOfHeader(ScheduleAxisVisual visual)
    {
        var group = new GeometryGroup();
        var axis = visual.Axis;
        for (var date = axis.Start.AddDays(1); date <= axis.End; date = date.AddDays(1))
        {
            var isBoundary = axis.Unit switch
            {
                ScheduleUnit.Day => true,
                ScheduleUnit.Week => date.DayOfWeek == DayOfWeek.Monday,
                _ => date.Day == 1,
            };
            if (isBoundary)
            {
                var x = axis.X(date);
                group.Children.Add(new LineGeometry { StartPoint = new Windows.Foundation.Point(x, LineHeight), EndPoint = new Windows.Foundation.Point(x, Height) });
            }
        }
        return group;
    }

    /// <summary>曜日の 1 文字 (PC の言語に依らず日本語)</summary>
    /// <param name="date">日付</param>
    /// <returns>月・火・水・木・金・土・日のどれか</returns>
    private static string JapaneseWeekday(DateOnly date) => "日月火水木金土"[(int)date.DayOfWeek].ToString();

    /// <summary>文字を作る</summary>
    /// <param name="text">文字</param>
    /// <param name="size">文字の大きさ</param>
    /// <param name="weight">文字の太さ</param>
    /// <param name="width">幅 (指定すると、中央に寄せる)</param>
    /// <returns>文字の部品</returns>
    private static TextBlock Text(string text, double size, Windows.UI.Text.FontWeight weight, double? width = null)
        => new()
        {
            Text = text,
            FontSize = size,
            FontWeight = weight,
            Width = width ?? double.NaN,
            TextAlignment = width is null ? TextAlignment.Left : TextAlignment.Center,
            IsHitTestVisible = false,
        };
}
