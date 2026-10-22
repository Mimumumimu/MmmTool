using System.ComponentModel;
using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using MmmTool.WorkItems.Core;
using Path = Microsoft.UI.Xaml.Shapes.Path;

namespace MmmTool.WorkItems.Schedule;

/// <summary>スケジュールの 1 行ぶんの、日付の棒を描くセル (休みの日の背景・日付の線・今日の線・予定の棒・実績の棒)</summary>
/// <remarks>
/// 行 (<see cref="ScheduleRow"/>)が変わったとき・描画に関わる値が変わったとき (<see cref="ScheduleRow.VisualVersion"/>)・テーマが変わったときに、描き直す。
/// 休みの日の背景と日付の線の図形は、セルが持ち、軸が変わったときだけ作り直す (位置は、軸 (<see cref="ScheduleAxisVisual"/>)が 1 回だけ数えて、全セルで使う)。
/// 1 行に、予定の棒 (太い背景)と、実績の棒 (細く、上に重ねる)を重ねる。クリックは通す (行の選択は、表が行の上で受ける)。
/// </remarks>
public sealed class ScheduleTimelineCell : Canvas
{
    /// <summary>予定の棒の上端</summary>
    private const double PlanBarTop = 3;

    /// <summary>予定の棒の高さ</summary>
    private const double PlanBarHeight = 30;

    /// <summary>開始日だけ・期限日だけの作業の、印の幅</summary>
    private const double OpenMarkWidth = 7;

    /// <summary>実績の棒の上端 (予定の棒の中央に重ねる)</summary>
    private const double ActualBarTop = 9;

    /// <summary>実績の棒の高さ</summary>
    private const double ActualBarHeight = 18;

    /// <summary>実績の棒の左右の内側への寄せ幅の上限 (予定の棒の端の余白 1 と、期限切れの枠 2 を避け、枠に被らせない)</summary>
    private const double ActualBarInset = 4;

    /// <summary>実績の時間の文字の標準の大きさ</summary>
    private const double HoursFontSize = 13;

    /// <summary>実績の時間の文字のフォント</summary>
    /// <remarks>等幅にして、同じ桁数の数字を同じ大きさにそろえる。縦の位置は、このフォントの寸法 (<see cref="HoursAscent"/>・<see cref="HoursDigitHeight"/>)から、大きさごとに計算する。</remarks>
    private static readonly FontFamily HoursFont = new("BIZ UDGothic, MS Gothic, Consolas");

    /// <summary>実績の時間の文字の、行の上端からベースラインまでの長さ</summary>
    /// <remarks>フォントの大きさに対する比。BIZ UDGothic の 1802 / 2048。</remarks>
    private const double HoursAscent = 1802.0 / 2048;

    /// <summary>実績の時間の数字の高さ</summary>
    /// <remarks>フォントの大きさに対する比。BIZ UDGothic の大文字の高さ 1567 / 2048。</remarks>
    private const double HoursDigitHeight = 1567.0 / 2048;

    /// <summary>実績の棒の中の、数字を置ける高さ</summary>
    /// <remarks>棒の高さから、枠の上下を引いた値。</remarks>
    private const double HoursInnerHeight = ActualBarHeight - 2;

    /// <summary>実績の時間の、大きさごとの上端の位置</summary>
    /// <remarks>大きさごとに 1 回だけ計算して使い回す。</remarks>
    private static readonly Dictionary<double, double> HoursTops = [];

    /// <summary>実績の時間の 1 文字の幅</summary>
    /// <remarks>標準の大きさで 1 回だけ測って使い回す。等幅なので、文字数に掛けて幅にする。</remarks>
    private static double _hoursCharWidth;

    /// <summary>実績の時間の文字を縮めるときの下限</summary>
    private const double MinHoursFontSize = 8;

    /// <summary>実績の時間の文字の左右に空ける幅</summary>
    /// <remarks>0 にする。数字は棒の縦の中央にあり、角の丸みに被らない。文字の幅に、左右の余白が元から含まれる。</remarks>
    private const double HoursSideMargin = 0;

    /// <summary>行 (<see cref="Row"/>)の依存関係プロパティ</summary>
    public static readonly DependencyProperty RowProperty = DependencyProperty.Register(
        nameof(Row),
        typeof(ScheduleRow),
        typeof(ScheduleTimelineCell),
        new PropertyMetadata(null, (d, e) => ((ScheduleTimelineCell)d).OnRowChanged((ScheduleRow?)e.OldValue, (ScheduleRow?)e.NewValue)));

    /// <summary>土曜の背景 (このセルが持つ。図形は、軸が変わったときだけ作り直す)</summary>
    private readonly Path _saturdays = new();

    /// <summary>日曜・祝日の背景 (このセルが持つ。図形は、軸が変わったときだけ作り直す)</summary>
    private readonly Path _sundays = new();

    /// <summary>日付の区切りの線 (このセルが持つ。図形は、軸が変わったときだけ作り直す)</summary>
    private readonly Path _lines = new() { StrokeThickness = 1 };

    /// <summary>休みの日の背景と日付の線の図形を作った軸 (違う軸になったら、図形を作り直す)</summary>
    private ScheduleAxisVisual? _shapesFor;

    /// <summary>セルを作る</summary>
    public ScheduleTimelineCell()
    {
        Height = ScheduleAxisVisual.RowHeight;
        IsHitTestVisible = false;
        ActualThemeChanged += (_, _) => Redraw();

        // 表に載る前の ActualTheme は、アプリの既定のテーマを返すことがある。載ったあとに、正しいテーマで描き直す
        Loaded += (_, _) => Redraw();
    }

    /// <summary>描く行</summary>
    public ScheduleRow? Row
    {
        get => (ScheduleRow?)GetValue(RowProperty);
        set => SetValue(RowProperty, value);
    }

    /// <summary>行が差し替わったら、前の行の通知を外し、新しい行の通知を受けて、描き直す</summary>
    /// <param name="oldRow">前の行</param>
    /// <param name="newRow">新しい行</param>
    private void OnRowChanged(ScheduleRow? oldRow, ScheduleRow? newRow)
    {
        if (oldRow is not null)
        {
            oldRow.PropertyChanged -= OnRowPropertyChanged;
        }
        if (newRow is not null)
        {
            newRow.PropertyChanged += OnRowPropertyChanged;
        }
        Redraw();
    }

    /// <summary>描画に関わる値が変わったら、描き直す</summary>
    /// <param name="sender">行</param>
    /// <param name="e">変更された値の名前</param>
    private void OnRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ScheduleRow.VisualVersion))
        {
            Redraw();
        }
    }

    /// <summary>行の内容を、すべて描き直す</summary>
    private void Redraw()
    {
        Children.Clear();
        if (Row is not { Axis: { } visual } row)
        {
            return;
        }

        var axis = visual.Axis;
        var palette = SchedulePalette.For(ActualTheme);
        Width = axis.Width;

        // 図形は、軸が変わったときだけ作る (行を使い回して描き直すたびには、作り直さない)
        if (!ReferenceEquals(_shapesFor, visual))
        {
            _saturdays.Data = visual.CreateSaturdays();
            _sundays.Data = visual.CreateSundays();
            _lines.Data = visual.CreateLines();
            _shapesFor = visual;
        }
        _saturdays.Fill = palette.SaturdayFill;
        _sundays.Fill = palette.SundayFill;
        _lines.Stroke = palette.Line;
        if (axis.ShowsOffDays)
        {
            Children.Add(_saturdays);
            Children.Add(_sundays);
        }
        Children.Add(_lines);
        AddTodayLine(axis, palette, row.Today);
        AddPlan(axis, palette, row);
        AddActual(axis, palette, row);
    }

    /// <summary>今日の列の中央に、縦の線を足す (棒と数字の下に描く)</summary>
    /// <param name="axis">日付の軸</param>
    /// <param name="palette">色</param>
    /// <param name="today">今日</param>
    private void AddTodayLine(ScheduleAxis axis, SchedulePalette palette, DateOnly today)
    {
        if (today < axis.Start || today > axis.End)
        {
            return;
        }

        var line = new Rectangle { Width = 2, Height = ScheduleAxisVisual.RowHeight, Fill = palette.Today, Opacity = 0.6 };
        SetLeft(line, axis.X(today) + (axis.DayWidth / 2) - 1);
        Children.Add(line);
    }

    /// <summary>予定の棒 (開始日から期限日まで。1 色)を足す</summary>
    /// <param name="axis">日付の軸</param>
    /// <param name="palette">色</param>
    /// <param name="row">行</param>
    /// <remarks>
    /// 開始日だけの作業は、開始日の左端に印と、右へ短い点線。期限だけの作業は、期限日の右端に印と、左へ短い点線 (同じ形で、点線の向きだけ逆)。日付が 1 つも無い作業は、棒を出さない。
    /// 期限切れの作業は、危険の色の枠を付ける。
    /// </remarks>
    private void AddPlan(ScheduleAxis axis, SchedulePalette palette, ScheduleRow row)
    {
        var isGroup = row.IsGroup;
        var plan = isGroup ? palette.GroupPlan : palette.Plan;

        if (row.PlanStart is { } start && row.PlanEnd is { } end && start <= end)
        {
            var left = axis.X(start) + 1;
            var width = Math.Max(2, axis.X(end) + axis.DayWidth - axis.X(start) - 2);
            Children.Add(Bar(left, PlanBarTop, width, PlanBarHeight, 5, plan));
            if (row.IsOverdue)
            {
                var frame = Bar(left, PlanBarTop, width, PlanBarHeight, 5, null);
                frame.BorderBrush = palette.Critical;
                frame.BorderThickness = new Thickness(2);
                Children.Add(frame);
            }
        }
        else if (row.PlanEnd is { } dueOnly)
        {
            // 開始日だけの作業と同じ形 (印と点線)。印は期限日の右端、点線は、その左へ伸ばす
            var markLeft = axis.X(dueOnly) + axis.DayWidth - 1 - OpenMarkWidth;
            Children.Add(Bar(markLeft, PlanBarTop, OpenMarkWidth, PlanBarHeight, 3, plan));
            Children.Add(OpenEndLine(markLeft - 4, markLeft - 32, plan));
        }
        else if (row.PlanStart is { } startOnly)
        {
            var left = axis.X(startOnly) + 1;
            Children.Add(Bar(left, PlanBarTop, OpenMarkWidth, PlanBarHeight, 3, plan));
            Children.Add(OpenEndLine(left + OpenMarkWidth + 4, left + OpenMarkWidth + 32, plan));
        }
    }

    /// <summary>実績の棒 (連続した日ごと。日の単位では、日ごとの時間の数字入り)を足す</summary>
    /// <param name="axis">日付の軸</param>
    /// <param name="palette">色</param>
    /// <param name="row">行</param>
    private void AddActual(ScheduleAxis axis, SchedulePalette palette, ScheduleRow row)
    {
        foreach (var run in row.Runs)
        {
            // 1 日の幅が狭い単位 (月など)で、棒の幅が負にならないよう、寄せる幅は 1 日の幅の 4 分の 1 までにする
            var inset = Math.Min(ActualBarInset, axis.DayWidth / 4);
            var bar = Bar(axis.X(run.Start) + inset, ActualBarTop, (run.Hours.Count * axis.DayWidth) - (2 * inset), ActualBarHeight, 4, palette.Actual);
            bar.BorderBrush = palette.ActualEdge;
            bar.BorderThickness = new Thickness(1);
            if (axis.ShowsHours)
            {
                var days = new Grid();

                // 1 日ぶんの欄の幅は、棒の幅から枠の左右を引いて、日数で割って求める。文字は、この幅に収まるまで縮める
                var columnWidth = (bar.Width - (2 * bar.BorderThickness.Left)) / run.Hours.Count;
                for (var i = 0; i < run.Hours.Count; i++)
                {
                    days.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                    // 実績の無い日は、数字を出さない (棒は、空いた日も含めて 1 本につなぐ)
                    if (run.Hours[i] <= 0)
                    {
                        continue;
                    }

                    var text = run.Hours[i].ToString("0.##", CultureInfo.InvariantCulture);
                    var fontSize = FittedFontSize(text, columnWidth);
                    var label = new TextBlock
                    {
                        Text = text,
                        FontFamily = HoursFont,
                        FontSize = fontSize,
                        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                        Foreground = palette.ActualText,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Top,
                        Margin = new Thickness(0, HoursTop(fontSize), 0, 0),
                    };
                    Grid.SetColumn(label, i);
                    days.Children.Add(label);
                }
                bar.Child = days;
            }
            Children.Add(bar);
        }
    }

    /// <summary>実績の時間の文字が、1 日ぶんの欄の幅に収まる大きさを求める</summary>
    /// <param name="text">時間の文字</param>
    /// <param name="columnWidth">1 日ぶんの欄の幅</param>
    /// <returns>フォントの大きさ</returns>
    /// <remarks>収まるなら標準の大きさにし、収まらないときは <see cref="MinHoursFontSize"/> を下限に縮める。</remarks>
    private static double FittedFontSize(string text, double columnWidth)
    {
        if (_hoursCharWidth == 0)
        {
            var probe = new TextBlock { Text = "0", FontFamily = HoursFont, FontSize = HoursFontSize, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
            probe.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
            _hoursCharWidth = probe.DesiredSize.Width;
        }

        var width = text.Length * _hoursCharWidth;
        var available = columnWidth - (2 * HoursSideMargin);
        if (width <= available)
        {
            return HoursFontSize;
        }

        // 端数の大きさだと、文字と余白が半ピクセルずれて縦の位置が揃わないので、整数に切り下げる
        return Math.Max(MinHoursFontSize, Math.Floor(HoursFontSize * available / width));
    }

    /// <summary>実績の時間の文字の、棒の中での上端の位置を求める</summary>
    /// <param name="fontSize">フォントの大きさ</param>
    /// <returns>棒の中の上端から、文字の枠の上端までの長さ (整数のピクセル)</returns>
    /// <remarks>
    /// 数字の真ん中が、棒の中の高さの中央に来る位置を、ベースラインの丸めまで含めて計算する。
    /// 文字はベースラインが整数のピクセルに丸められるので、丸めたあとの数字の真ん中が中央に最も近い整数を選ぶ。
    /// 大きさごとに 1 回だけ計算して使い回す。
    /// </remarks>
    private static double HoursTop(double fontSize)
    {
        if (!HoursTops.TryGetValue(fontSize, out var top))
        {
            top = Math.Round((HoursInnerHeight / 2) - Math.Round(HoursAscent * fontSize) + (HoursDigitHeight * fontSize / 2));
            HoursTops[fontSize] = top;
        }

        return top;
    }

    /// <summary>期間の片方が決まっていない側へ伸ばす点線を作る (予定の棒の高さの中央)</summary>
    /// <param name="fromX">印に近い側の位置</param>
    /// <param name="toX">印から遠い側の位置</param>
    /// <param name="stroke">線の色</param>
    /// <returns>点線</returns>
    private static Line OpenEndLine(double fromX, double toX, Brush stroke)
    {
        var line = new Line
        {
            X1 = fromX,
            X2 = toX,
            Y1 = PlanBarTop + (PlanBarHeight / 2),
            Y2 = PlanBarTop + (PlanBarHeight / 2),
            Stroke = stroke,
            StrokeThickness = 2,
        };
        line.StrokeDashArray.Add(2);
        line.StrokeDashArray.Add(2);
        return line;
    }

    /// <summary>棒 (角の丸い長方形)を作る</summary>
    /// <param name="left">左端の位置</param>
    /// <param name="top">上端の位置</param>
    /// <param name="width">幅</param>
    /// <param name="height">高さ</param>
    /// <param name="radius">角の丸み</param>
    /// <param name="fill">塗りつぶしの色 (枠だけなら null)</param>
    /// <returns>棒</returns>
    private static Border Bar(double left, double top, double width, double height, double radius, Brush? fill)
    {
        var bar = new Border { Width = width, Height = height, CornerRadius = new CornerRadius(radius), Background = fill };
        SetLeft(bar, left);
        SetTop(bar, top);
        return bar;
    }
}
