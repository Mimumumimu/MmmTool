using System.ComponentModel;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using MmmSdk.Core.Utilities;
using MmmSdk.WinUI.Controls;
using MmmSdk.WinUI.Utilities;
using MmmTool.WorkItems.Core;
using Windows.System;

namespace MmmTool.WorkItems.Main;

/// <summary>作業管理のページ</summary>
/// <remarks>
/// 表 (<see cref="TreeGridView"/>)の列と右クリックのメニューを組み立て、セルの入力の確定を ViewModel に渡す。
/// 入力欄は表の行を使い回すので、確定の操作 (フォーカスが外れたとき・Enter)だけを保存に結び付け、値の変更のイベント (選択・日付の変更)には結び付けない
/// (使い回しの値の差し替えで、保存が走らないようにするため)。
/// </remarks>
public sealed partial class WorkItemPage : Page
{
    /// <summary>列の定義 (キー・見出し・幅・固定か・毎日入力する列か)</summary>
    private readonly List<TreeGridColumn> _columns;

    /// <summary>状態と進捗度・実績の食い違いを知らせる吹き出し</summary>
    private readonly WarningBubble _warningBubble = new();

    /// <summary>入力日の実績の列</summary>
    private readonly TreeGridColumn _actualDayColumn;

    /// <summary>入力日の備考の列</summary>
    private readonly TreeGridColumn _dayNoteColumn;

    /// <summary>毎日入力する列のキー (見出しの色を付ける)</summary>
    /// <summary>確定を渡し済みの入力欄 (入力欄ごとに 1 回だけ保存するため)</summary>
    private readonly System.Runtime.CompilerServices.ConditionalWeakTable<TextBox, object?> _committedBoxes = [];

    private static readonly HashSet<string> DailyKeys = ["Status", "Progress", "ActualDay", "DayNote", "Note"];

    /// <summary>日報メモの入力欄に、一度に見せる行数 (入力欄の高さは、この行数から計算する)</summary>
    private const int DayNoteLines = 5;

    /// <summary>ページの ViewModel</summary>
    public WorkItemViewModel ViewModel { get; }

    /// <summary>ページを作る</summary>
    /// <param name="viewModel">ページの ViewModel</param>
    public WorkItemPage(WorkItemViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        ProjectPickerControl.Attach(ViewModel.ProjectPicker);
        DayNoteBox.Loaded += (_, _) => FitDayNoteHeight();

        _actualDayColumn = Column("ActualDay", "実績 (日)", 100, "ActualDayCell");
        _dayNoteColumn = Column("DayNote", "備考 (日)", 220, "DayNoteCell");
        _columns =
        [
            Column("Name", "名前", 260, "NameCell", frozen: true, tree: true),
            Column("Assignee", "担当", 96, "AssigneeCell", frozen: true),
            Column("Priority", "優先度", 72, "PriorityCell", frozen: true),
            Column("Start", "開始日", 132, "StartCell", frozen: true),
            Column("Due", "期限日", 132, "DueCell", frozen: true),
            Column("Completed", "完了日", 96, "CompletedCell", frozen: true),
            Column("Planned", "予定 (累計)", 84, "PlannedCell", frozen: true),
            Column("ActualTotal", "実績 (累計)", 96, "ActualTotalCell", frozen: true),
            Column("Link", "リンク", 72, "LinkCell", frozen: true),
            Column("Status", "状態", 104, "StatusCell"),
            Column("Progress", "進捗度", 190, "ProgressCell"),
            _actualDayColumn,
            _dayNoteColumn,
            Column("Note", "備考", 260, "NoteCell"),
        ];
        // 案件・グループの行の備考は入力できるので、行の青い背景を付けず、作業の行の備考と同じ見た目にする
        _columns.First(c => c.Key == "Note").KeepsBaseBackground = true;
        _columns.First(c => c.Key == "Planned").HeaderToolTip = "作業全体の予定の工数 (時間)";
        foreach (var column in _columns)
        {
            TreeGrid.Columns.Add(column);
        }

        // 曜日は、PC の言語に依らず日本語で出す
        InputDatePicker.Language = "ja-JP";
        InputDatePicker.DateFormat = "{year.full}/{month.integer(2)}/{day.integer(2)} ({dayofweek.abbreviated})";
        InputDatePicker.Date = ToOffset(ViewModel.InputDate);

        var rowMenu = (MenuFlyout)Resources["RowMenu"];
        rowMenu.Opening += OnRowMenuOpening;
        TreeGrid.RowContextFlyout = rowMenu;
        TreeGrid.CellInvoked += OnCellInvoked;
        TreeGrid.EditCanceled += (_, e) => (e.Row as WorkItemRow)?.Revert();
        TreeGrid.EditEnding += OnEditEnding;
        TreeGrid.ItemsSource = ViewModel.Rows;
        TreeGrid.RowToggleRequested += (_, e) => ViewModel.Toggle((WorkItemRow)e.Row);
        Unloaded += (_, _) => ViewModel.Suspend();
        TreeGrid.SelectedRowChanged += (_, _) => ViewModel.SelectedRow = TreeGrid.SelectedRow as WorkItemRow;

        BuildColumnsFlyout();
        ApplyColumns(fitAll: true);

        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        ViewModel.ColumnVisibilityChanged += (_, _) => ApplyColumns();
        ViewModel.RowsRebuilt += (_, _) =>
        {
            if (FitColumns())
            {
                TreeGrid.RefreshColumnWidths();
            }
        };
        ViewModel.FocusNameRequested += (_, row) => TreeGrid.FocusCell(row, "Name");
        ViewModel.WarningRaised += OnWarningRaised;
        ActualThemeChanged += (_, _) => ApplyColumns();

        // 次の操作を始めたら、警告の吹き出しは、すぐ消す (ほかの処理が先に受け取った操作も含む)
        AddHandler(PointerPressedEvent, new PointerEventHandler((_, _) => _warningBubble.Hide()), handledEventsToo: true);
        AddHandler(KeyDownEvent, new KeyEventHandler((_, _) => _warningBubble.Hide()), handledEventsToo: true);
        Unloaded += (_, _) => _warningBubble.Hide();
    }

    /// <summary>状態と進捗度・実績の食い違いを、入力したセルの下に短く出す</summary>
    /// <param name="sender">ViewModel</param>
    /// <param name="warning">警告 (行・列・文)</param>
    /// <remarks>保存の直後は、行の表示を作り直している途中なので、表示が整ってから、セルの位置を引く。</remarks>
    private void OnWarningRaised(object? sender, WorkWarning warning)
        => DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
        {
            if (TreeGrid.FindCell(warning.Row, warning.ColumnKey) is { } cell)
            {
                _warningBubble.Show(cell, warning.Message);
            }
        });

    /// <summary>日報メモの入力欄の高さを、<see cref="DayNoteLines"/> 行ちょうどが入る高さにする</summary>
    /// <remarks>
    /// 高さ = 行数 × 1 行の高さ + 上下の余白 + 枠。1 行の高さは、入力欄と同じフォントの <see cref="TextBlock"/> を 1 行ぶん測って求める
    /// (<see cref="TextBox"/> には、行の高さの指定が無い。測るので、画面の拡大率やフォントが変わってもずれない)。
    /// 入力欄を作ったあと (<see cref="FrameworkElement.Loaded"/>)に呼ぶ。スタイルが当たる前は、フォントの大きさが決まらないため。
    /// </remarks>
    private void FitDayNoteHeight()
    {
        var probe = new TextBlock { FontFamily = DayNoteBox.FontFamily, FontSize = DayNoteBox.FontSize, Text = "あ" };
        probe.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
        DayNoteBox.Height = (probe.DesiredSize.Height * DayNoteLines)
            + DayNoteBox.Padding.Top + DayNoteBox.Padding.Bottom
            + DayNoteBox.BorderThickness.Top + DayNoteBox.BorderThickness.Bottom;
    }

    /// <summary>ページを開いたときに、保存先から読み直す</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">イベントの情報</param>
    private async void OnLoaded(object sender, RoutedEventArgs e) => await ViewModel.InitializeAsync();

    /// <summary>日付の選択欄が入力用に切り替わったら、すぐカレンダーを開く</summary>
    /// <param name="sender">日付の選択欄</param>
    /// <param name="e">イベントの情報</param>
    private void OnDateLoaded(object sender, RoutedEventArgs e)
    {
        // クリックの処理の途中で開くと、そのクリックの続きとして扱われるので、処理が済んでから開く
        if (sender is CalendarDatePicker picker)
        {
            DispatcherQueue.TryEnqueue(() => picker.IsCalendarOpen = true);
        }
    }

    /// <summary>カレンダーが開いたとき、日付が空なら、入力日 (終了日は開始日があれば開始日)の月を表示する</summary>
    /// <param name="sender">日付の選択欄</param>
    /// <param name="e">イベントの情報</param>
    /// <remarks>日付が空のままだと、カレンダーはシステムの今日の月を表示するため。選択はしない (表示する月だけ変える)。</remarks>
    private void OnDateOpened(object? sender, object e)
    {
        if (sender is not CalendarDatePicker { Date: null } picker || TreeGridView.GetRow(picker) is not WorkItemRow row)
        {
            return;
        }

        var basis = ColumnKey(picker) == "Due" && row.StartValue is { } start ? start : ToOffset(ViewModel.InputDate);

        // 開いた直後は、ポップアップの中身がまだ無いことがあるので、処理が済んでから探す
        DispatcherQueue.TryEnqueue(() =>
        {
            foreach (var popup in VisualTreeHelper.GetOpenPopupsForXamlRoot(XamlRoot))
            {
                if (FindCalendarView(popup.Child) is { } calendar)
                {
                    calendar.SetDisplayDate(basis);
                    return;
                }
            }
        });
    }

    /// <summary>要素の中から、最初のカレンダーを探す</summary>
    /// <param name="element">探し始める要素</param>
    /// <returns>見つかったカレンダー。無ければ <see langword="null"/></returns>
    private static CalendarView? FindCalendarView(DependencyObject? element)
    {
        if (element is CalendarView calendar)
        {
            return calendar;
        }

        for (var i = 0; element is not null && i < VisualTreeHelper.GetChildrenCount(element); i++)
        {
            if (FindCalendarView(VisualTreeHelper.GetChild(element, i)) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    /// <summary>カレンダーが閉じたら、表示用に戻す</summary>
    /// <param name="sender">日付の選択欄</param>
    /// <param name="e">イベントの情報</param>
    private void OnDateClosed(object? sender, object e)
    {
        if (sender is CalendarDatePicker picker)
        {
            TreeGridView.EndEdit(picker);
        }
    }

    #region 列

    /// <summary>列の定義を作る</summary>
    /// <param name="key">列のキー</param>
    /// <param name="header">見出し</param>
    /// <param name="width">幅</param>
    /// <param name="templateKey">セルの見た目のリソースのキー</param>
    /// <param name="frozen">固定の列か</param>
    /// <param name="tree">木構造の列か</param>
    /// <returns>列の定義</returns>
    private TreeGridColumn Column(string key, string header, double width, string templateKey, bool frozen = false, bool tree = false)
        => new()
        {
            Key = key,
            Header = header,
            Width = width,
            IsFrozen = frozen,
            IsTree = tree,
            CellTemplate = (DataTemplate)Resources[templateKey],
            EditTemplate = Resources.TryGetValue(EditKey(templateKey), out var edit) ? (DataTemplate)edit : null,
            IsInvokable = key is "Priority" or "Status" or "Progress" or "Assignee" or "Link",
        };

    /// <summary>表示用のセルの見た目のキーから、入力用のキーを返す</summary>
    /// <param name="templateKey">表示用のキー (…Cell)</param>
    /// <returns>入力用のキー (…Edit)</returns>
    private static string EditKey(string templateKey) => templateKey[..^"Cell".Length] + "Edit";

    /// <summary>設定と入力日に合わせて、列の表示・見出し・色を直し、表に反映する</summary>
    /// <param name="fitAll">表示しているすべての列の幅を測るか (作った直後。false なら、新しく表示する列だけ)</param>
    private void ApplyColumns(bool fitAll = false)
    {
        var lineBrush = (Brush)Resources["DailyLineBrush"];
        var shown = new List<TreeGridColumn>();
        foreach (var column in _columns)
        {
            var wasVisible = column.IsVisible;
            column.IsVisible = column.Key == "Name" || (ViewModel.IsColumnVisible(column.Key) && (column.Key != "Assignee" || ViewModel.HasAssignees));
            column.HeaderUnderline = DailyKeys.Contains(column.Key) ? lineBrush : null;
            if (column.IsVisible && !wasVisible)
            {
                shown.Add(column);
            }
        }

        // 幅を測るのは、新しく表示する列だけ (ほかの列の中身は変わらない。表示していない間に変わった中身は、表示するときに測る)
        FitColumns(fitAll ? null : shown);
        TreeGrid.RefreshColumns();
    }

    /// <summary>表示している列の幅を、中身に合わせる</summary>
    /// <param name="only">測る列 (null なら、表示しているすべての列)</param>
    /// <returns>幅が変わった列があるか</returns>
    /// <remarks>
    /// 見出し・選択肢・今の行の文字のうち、いちばん長いものに、余白を足した幅にする (備考だけは、上限を設ける)。
    /// 表示を変える操作 (行の追加・入力の確定)のあとに呼ぶ。表示していない列は測らない (行の数 × 列の数だけ、文字の幅を測るので、重いため)。
    /// </remarks>
    private bool FitColumns(IReadOnlyCollection<TreeGridColumn>? only = null)
    {
        var rows = ViewModel.Rows;
        var changed = false;
        foreach (var column in _columns)
        {
            if (!column.IsVisible || (only is not null && !only.Contains(column)))
            {
                continue;
            }

            var width = column.Key switch
            {
                "Name" => Math.Max(HeaderWidth(column), rows.Select(r => 4 + (r.Level * TreeGrid.IndentWidth) + 22 + TextWidth(r.Name, WorkItemOptions.NameWeight(r.IsGroup, r.Level), r.IsProject ? WorkItemOptions.ProjectFontSize : 14) + 40 + (r.ShowsReportExcludedMark ? ReportExcludedMarkWidth : 0)).DefaultIfEmpty(0).Max()),
                "Assignee" => Math.Max(HeaderWidth(column), WidestWidth(rows.Select(r => r.AssigneeText)) + 24),
                "Priority" => Math.Max(HeaderWidth(column), WidestWidth(rows.Select(r => r.PriorityText)) + 24),
                "Start" or "Due" => Math.Max(HeaderWidth(column), TextWidth("2026/10/10", Microsoft.UI.Text.FontWeights.Normal) + 52),
                "Planned" => Math.Max(HeaderWidth(column), TextWidth("時間 (H)", Microsoft.UI.Text.FontWeights.Normal) + 24),
                "Completed" => Math.Max(HeaderWidth(column), TextWidth("2026/10/10", Microsoft.UI.Text.FontWeights.Normal) + 24),
                "ActualTotal" => Math.Max(HeaderWidth(column), TextWidth("0000.00", Microsoft.UI.Text.FontWeights.Normal) + 24),
                "Link" => Math.Max(HeaderWidth(column), TextWidth("リンク", Microsoft.UI.Text.FontWeights.Normal) + 24),
                "Status" => Math.Max(HeaderWidth(column), WidestWidth(rows.Select(r => r.StatusText)) + 24),
                "Progress" => Math.Max(HeaderWidth(column), WidestWidth(rows.Select(r => r.ProgressCellText)) + 24),
                "ActualDay" => Math.Max(HeaderWidth(column), TextWidth("時間 (H)", Microsoft.UI.Text.FontWeights.Normal) + 24),
                "DayNote" => Math.Min(NoteMaxWidth, Math.Max(HeaderWidth(column), rows.Select(r => TextWidth(r.DayNoteFirstLine, Microsoft.UI.Text.FontWeights.Normal) + 28).DefaultIfEmpty(0).Max())),
                "Note" => Math.Min(NoteMaxWidth, Math.Max(HeaderWidth(column), rows.Select(r => TextWidth(r.NoteFirstLine, Microsoft.UI.Text.FontWeights.Normal) + 28).DefaultIfEmpty(0).Max())),
                _ => column.Width,
            };
            width = Math.Ceiling(width);
            if (Math.Abs(column.Width - width) >= 1)
            {
                column.Width = width;
                changed = true;
            }
        }
        return changed;
    }

    /// <summary>備考の列の幅の上限 (これより長い文は、省略して出す)</summary>
    private const double NoteMaxWidth = 320;

    /// <summary>名前のセルに出す、作業報告に含めない印の幅 (アイコンの大きさ 14 に、文字との間の余白を足した値)</summary>
    private const double ReportExcludedMarkWidth = 24;

    /// <summary>文字の幅の測定に使う部品</summary>
    private readonly TextBlock _measurer = new();

    /// <summary>文字を表示したときの幅を測る</summary>
    /// <param name="text">文字</param>
    /// <param name="weight">文字の太さ</param>
    /// <param name="fontSize">文字の大きさ</param>
    /// <returns>幅</returns>
    private double TextWidth(string text, Windows.UI.Text.FontWeight weight, double fontSize = 14)
    {
        _measurer.Text = text;
        _measurer.FontWeight = weight;
        _measurer.FontSize = fontSize;
        _measurer.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
        return _measurer.DesiredSize.Width;
    }

    /// <summary>いちばん長い選択肢の幅を測る</summary>
    /// <param name="texts">選択肢</param>
    /// <returns>幅</returns>
    private double WidestWidth(IEnumerable<string> texts)
        => texts.Select(t => TextWidth(t, Microsoft.UI.Text.FontWeights.Normal)).DefaultIfEmpty(0).Max();

    /// <summary>列の見出しに必要な幅を測る</summary>
    /// <param name="column">列</param>
    /// <returns>見出しの文字の幅に、左右の余白を足したもの</returns>
    private double HeaderWidth(TreeGridColumn column) => TextWidth(column.Header, Microsoft.UI.Text.FontWeights.SemiBold) + 28;

    /// <summary>「表示列」のボタンに、列ごとのチェックを並べた入れ物を付ける</summary>
    /// <remarks>続けて何列も切り替えられるよう、メニューではなく、開いたままの入れ物にする。名前の列は、常に出す。</remarks>
    private void BuildColumnsFlyout()
    {
        var panel = new StackPanel { Spacing = 0 };
        foreach (var column in _columns.Where(c => c.Key != "Name" && (c.Key != "Assignee" || ViewModel.HasAssignees)))
        {
            var key = column.Key;
            var check = new CheckBox
            {
                Content = column.Header,
                IsChecked = ViewModel.IsColumnVisible(key),
                MinWidth = 0,
            };
            check.Click += (_, _) => ViewModel.SetColumnVisible(key, check.IsChecked == true);
            panel.Children.Add(check);
        }
        ColumnsButton.Flyout = new Flyout { Content = panel, Placement = FlyoutPlacementMode.BottomEdgeAlignedRight };
    }

    #endregion

    #region 入力日

    /// <summary>入力日の日付が選ばれたら、ViewModel に反映する</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="args">日付の変更の情報</param>
    private void OnInputDateChanged(CalendarDatePicker sender, CalendarDatePickerDateChangedEventArgs args)
    {
        if (args.NewDate is { } date)
        {
            ViewModel.InputDate = DateOnly.FromDateTime(date.Date);
        }
        else
        {
            // 日付がクリアされたときは、今の入力日に戻す (入力日は、いつも決まっている)
            sender.Date = ToOffset(ViewModel.InputDate);
        }
    }

    /// <summary>日付を、日付の選択欄の値にする</summary>
    /// <param name="date">日付</param>
    /// <returns>その日の 0 時 (この PC のタイムゾーン)</returns>
    private static DateTimeOffset ToOffset(DateOnly date) => new(date.ToDateTime(TimeOnly.MinValue));

    /// <summary>ViewModel の値が変わったら、画面を合わせる</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">変更されたプロパティの情報</param>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(WorkItemViewModel.InputDate):
                if (InputDatePicker.Date?.Date != ViewModel.InputDate.ToDateTime(TimeOnly.MinValue))
                {
                    InputDatePicker.Date = ToOffset(ViewModel.InputDate);
                }
                break;
            case nameof(WorkItemViewModel.SelectedRow):
                if (!ReferenceEquals(TreeGrid.SelectedRow, ViewModel.SelectedRow))
                {
                    TreeGrid.SelectedRow = ViewModel.SelectedRow;
                }
                break;
        }
    }

    #endregion

    #region セルの入力

    /// <summary>文字の入力欄にフォーカスが入ったら、全体を選ぶ (そのまま打ち替えられるように)</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">イベントの情報</param>
    private void OnCellGotFocus(object sender, RoutedEventArgs e) => (sender as TextBox)?.SelectAll();

    /// <summary>時間の入力欄で、数字と小数点以外の入力を受け付けない</summary>
    /// <param name="sender">入力欄</param>
    /// <param name="args">入力前の文字の情報</param>
    /// <remarks>整数部 4 桁・小数部 2 桁まで (1・1.5・1.25 の形)。空は許す (なし)。</remarks>
    private void OnNumberBeforeTextChanging(TextBox sender, TextBoxBeforeTextChangingEventArgs args)
        => args.Cancel = !HoursPattern().IsMatch(args.NewText);

    /// <summary>入力中の時間として許す形</summary>
    /// <returns>正規表現</returns>
    [GeneratedRegex(@"^\d{0,4}(\.\d{0,2})?$")]
    private static partial Regex HoursPattern();

    /// <summary>日付の選択欄で日付が選ばれたら、確定する</summary>
    /// <param name="sender">日付の選択欄</param>
    /// <param name="args">日付の変更の情報</param>
    /// <remarks>
    /// 行の使い回しで値が差し替わったときも起きるので、行の値と同じなら何もしない。
    /// 日付をクリアするときは、行の右クリックのメニューの「日付をクリア」を使う。
    /// </remarks>
    private void OnCellDateChanged(CalendarDatePicker sender, CalendarDatePickerDateChangedEventArgs args)
    {
        if (TreeGridView.GetRow(sender) is WorkItemRow row)
        {
            var text = args.NewDate is { } date ? date.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture) : "";
            ViewModel.CommitTextAsync(row, ColumnKey(sender), text).Forget();
        }
    }

    /// <summary>文字の入力欄で Enter が押されたら、入力を確定する</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">キー入力の情報</param>
    private void OnTextCellKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter && sender is TextBox box)
        {
            e.Handled = true;
            CommitText(box);
            TreeGridView.EndEdit(box);
        }
    }

    /// <summary>文字の入力欄からフォーカスが外れたら、入力を確定する</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">イベントの情報</param>
    private void OnTextCellLostFocus(object sender, RoutedEventArgs e) => CommitText(sender);

    /// <summary>入力欄が属する列のキーを返す</summary>
    /// <param name="element">入力欄 (XAML の名前が、<c>Col_</c> に列のキーをつないだもの)</param>
    /// <returns>列のキー</returns>
    /// <remarks>名前をそのまま列のキーにしないのは、行のプロパティ (Name・Note など)と同じ名前だと、x:Bind が、どちらを指すか決められないため。</remarks>
    private static string ColumnKey(FrameworkElement element) => element.Name.StartsWith("Col_", StringComparison.Ordinal) ? element.Name[4..] : element.Name;

    /// <summary>文字の入力欄の入力を、保存に渡す</summary>
    /// <param name="sender">入力欄 (行は、表から引く。Name に列の名前が入っている)</param>
    private void CommitText(object sender)
    {
        if (sender is TextBox box && TreeGridView.GetRow(box) is WorkItemRow row)
        {
            CommitText(box, row);
        }
    }

    /// <summary>文字の入力欄の入力を、保存に渡す (同じ入力欄からは、1 回だけ)</summary>
    /// <param name="box">入力欄</param>
    /// <param name="row">入力欄の行</param>
    /// <remarks>
    /// Enter・フォーカスが外れた通知・入力欄を外す直前の通知 (<see cref="TreeGridView.EditEnding"/>)の、どれからも呼ばれるので、
    /// 同じ入力欄からの 2 回目は何もしない (1 回目の保存が終わる前は、行の値が古いので、値の比較では防げない)。
    /// 入力のたびに、入力欄は作り直されるので、入力欄ごとに 1 回で足りる。
    /// </remarks>
    private void CommitText(TextBox box, WorkItemRow row)
    {
        if (_committedBoxes.TryAdd(box, null))
        {
            ViewModel.CommitTextAsync(row, ColumnKey(box), box.Text).Forget();
        }
    }

    /// <summary>入力中のセルが、表示用に戻される直前に、入力した値を確定する</summary>
    /// <param name="sender">表</param>
    /// <param name="e">セルの情報</param>
    /// <remarks>
    /// ほかのセルや表の外のクリックでは、入力欄が外されたあとに、フォーカスが外れた通知が届くので、そのときは入力欄から行をたどれない。
    /// 入力欄がまだ表の中にあるこの時点で、行を渡されて確定する。
    /// </remarks>
    private void OnEditEnding(object? sender, TreeGridCellEventArgs e)
    {
        if (e.Row is WorkItemRow row && VisualTreeSearch.FindDescendant<TextBox>(e.Cell) is { } box)
        {
            CommitText(box, row);
        }
    }

    /// <summary>選択の列 (優先度・状態・進捗度)のセルがクリックされたら、選択肢のメニューを、セルの下に出す</summary>
    /// <param name="sender">表</param>
    /// <param name="e">セルの情報</param>
    /// <remarks>
    /// プルダウン (ComboBox)は、行ごとに置くと重く、表の中で入力の切り替えと組み合わせると、一覧の項目が選べなくなったため、使わない。
    /// 選択肢は短い一覧なので、メニューで選ぶ (今の値にチェックを付ける)。
    /// </remarks>
    private void OnCellInvoked(object? sender, TreeGridCellEventArgs e)
    {
        if (e.Row is not WorkItemRow row)
        {
            return;
        }

        if (e.ColumnKey == "Assignee")
        {
            ShowAssigneeMenu(row, e.Cell);
            return;
        }

        if (e.ColumnKey == "Link")
        {
            ViewModel.OpenLinkAsync(row).Forget();
            return;
        }

        var (options, current) = e.ColumnKey switch
        {
            "Priority" => (row.Choices.Priorities, row.PriorityIndex),
            "Status" => (row.Choices.Statuses, row.StatusIndex),
            "Progress" => (row.Choices.Progresses, row.ProgressIndex),
            _ => ((IReadOnlyList<WorkChoice>)[], -1),
        };

        var menu = new MenuFlyout();
        for (var i = 0; i < options.Count; i++)
        {
            var index = i;
            var item = new RadioMenuFlyoutItem { Text = options[i].Text, GroupName = e.ColumnKey, IsChecked = i == current };
            item.Click += (_, _) => ViewModel.CommitChoiceAsync(row, e.ColumnKey, index).Forget();
            menu.Items.Add(item);
        }
        menu.ShowAt(e.Cell, new FlyoutShowOptions { Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft });
    }

    /// <summary>担当者のメニューを、セルの下に出す</summary>
    /// <param name="row">行</param>
    /// <param name="cell">セル</param>
    private void ShowAssigneeMenu(WorkItemRow row, FrameworkElement cell)
    {
        var menu = new MenuFlyout();
        foreach (var assignee in ViewModel.AssigneeChoices)
        {
            var userId = assignee.UserId;
            var item = new RadioMenuFlyoutItem { Text = assignee.Name, GroupName = "Assignee", IsChecked = userId == row.AssigneeUserId };
            item.Click += (_, _) => ViewModel.CommitAssigneeAsync(row, userId).Forget();
            menu.Items.Add(item);
        }
        menu.ShowAt(cell, new FlyoutShowOptions { Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft });
    }

    /// <summary>リンクの入力欄を、指定の場所の下に出す</summary>
    /// <param name="row">行</param>
    /// <param name="target">入力欄を出す場所の要素</param>
    /// <remarks>Enter か、入力欄を閉じたときに確定する。Esc は、元のリンクのまま閉じる (保存しない)。空にして確定すると、リンクを消す。</remarks>
    private void ShowLinkEditor(WorkItemRow row, FrameworkElement target)
    {
        var box = new TextBox
        {
            Width = 360,
            MaxLength = WorkItemLimits.LinkMaxLength,
            Text = row.Link,
            PlaceholderText = "URL・フォルダ・ファイルのパス",
            IsSpellCheckEnabled = false,
            InputScope = new InputScope { Names = { new InputScopeName { NameValue = InputScopeNameValue.Url } } },
        };
        var flyout = new Flyout { Content = box, Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft };
        var cancelled = false;
        box.KeyDown += (_, e) =>
        {
            if (e.Key is VirtualKey.Enter or VirtualKey.Escape)
            {
                e.Handled = true;
                cancelled = e.Key == VirtualKey.Escape;
                flyout.Hide();
            }
        };
        flyout.Opened += (_, _) =>
        {
            box.Focus(FocusState.Programmatic);
            box.SelectAll();
        };
        flyout.Closed += (_, _) =>
        {
            if (!cancelled)
            {
                ViewModel.CommitLinkAsync(row, box.Text).Forget();
            }
        };
        // メニューの項目を押した処理の途中で別の入れ物を開くと、そのクリックの続きとして扱われて閉じるので、処理が済んでから開く
        DispatcherQueue.TryEnqueue(() => flyout.ShowAt(target));
    }

    /// <summary>備考の入力欄を開くとき、今の備考を入れる</summary>
    /// <param name="sender">開く入れ物</param>
    /// <param name="e">イベントの情報</param>
    /// <remarks>触れない作業 (完了して日がたった作業)の備考は、全文を読めるよう開くが、入力はできない。</remarks>
    private void OnNoteFlyoutOpening(object? sender, object e)
    {
        if (sender is Flyout { Target: Button button, Content: TextBox box } && TreeGridView.GetRow(button) is WorkItemRow row)
        {
            _noteTarget = button;
            box.IsReadOnly = row.IsLocked;
            box.Text = WorkItemViewModel.GetNote(row, ColumnKey(button));
        }
    }

    /// <summary>備考の入れ物を開いているボタン</summary>
    private Button? _noteTarget;

    /// <summary>備考の入力欄で Esc が押されたら、入力を取りやめる (元の備考に戻す。入れ物は、そのまま閉じる)</summary>
    /// <param name="sender">備考の入力欄</param>
    /// <param name="e">キー入力の情報</param>
    /// <remarks>閉じるときの確定では、元の備考と同じなので、保存されない。表のセルの Esc と同じ動き。</remarks>
    private void OnNoteKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Escape && sender is TextBox box && _noteTarget is { } button && TreeGridView.GetRow(button) is WorkItemRow row)
        {
            box.Text = WorkItemViewModel.GetNote(row, ColumnKey(button));
        }
    }

    /// <summary>備考の入力欄を開いたら、入力できる状態にする</summary>
    /// <param name="sender">開いた入れ物</param>
    /// <param name="e">イベントの情報</param>
    private void OnNoteFlyoutOpened(object? sender, object e)
    {
        if (sender is Flyout { Content: TextBox box })
        {
            box.Focus(FocusState.Programmatic);
            box.SelectionStart = box.Text.Length;
        }
    }

    /// <summary>備考の入力欄を閉じたら、入力を確定する</summary>
    /// <param name="sender">閉じた入れ物</param>
    /// <param name="e">イベントの情報</param>
    private void OnNoteFlyoutClosed(object? sender, object e)
    {
        if (sender is Flyout { Target: Button button, Content: TextBox box } && TreeGridView.GetRow(button) is WorkItemRow row)
        {
            ViewModel.CommitNoteAsync(row, ColumnKey(button), box.Text).Forget();
        }
    }

    #endregion

    #region 右クリックのメニュー

    /// <summary>行の右クリックのメニューを、その行に合わせて作る</summary>
    /// <param name="sender">メニュー</param>
    /// <param name="e">イベントの情報</param>
    private void OnRowMenuOpening(object? sender, object e)
    {
        if (sender is not MenuFlyout menu)
        {
            return;
        }

        menu.Items.Clear();
        if (TreeGridView.GetRow(menu.Target) is not WorkItemRow row)
        {
            return;
        }

        if (row.IsGroup)
        {
            AddItem(menu, "グループを追加", () => ViewModel.AddAsync(WorkRowKind.Group, row));
        }
        AddItem(menu, "作業を追加", () => ViewModel.AddAsync(WorkRowKind.Work, row));

        // 右クリックした所が、作業の日付のセルのときだけ (そのセルに固有の操作を、行全体の「リンクを編集…」より上に置く)
        if (row.IsWork && TreeGrid.ContextColumnKey is ("Start" or "Due") and { } dateKey)
        {
            menu.Items.Add(new MenuFlyoutSeparator());
            AddItem(menu, "日付をクリア", () => ViewModel.CommitTextAsync(row, dateKey, ""), !row.IsLocked);
        }

        // 「リンクを編集…」は、行のどこを右クリックしても出す (案件・グループ・作業とも)
        // 入力欄は、リンクのセルの下に出す (行の左端の下に出ると、視線が離れる)
        var onLinkCell = TreeGrid.ContextColumnKey == "Link" && TreeGrid.ContextCell is not null;
        var linkTarget = onLinkCell ? TreeGrid.ContextCell : TreeGrid.FindCell(row, "Link");
        if (linkTarget is not null)
        {
            menu.Items.Add(new MenuFlyoutSeparator());
            AddItem(menu, "リンクを編集…", () =>
            {
                ShowLinkEditor(row, linkTarget);
                return Task.CompletedTask;
            }, !row.IsLocked);

            // 「リンクをクリア」は、リンクのセルを右クリックしたときだけ出す
            if (onLinkCell)
            {
                AddItem(menu, "リンクをクリア", () => ViewModel.CommitLinkAsync(row, ""), row.HasLink && !row.IsLocked);
            }
        }

        // 定例の会議など、報告しない作業に付ける。作った直後に 1 回切り替える使い方なので、メニューだけに置く (印は名前のセルに出す)
        if (row.IsWork)
        {
            menu.Items.Add(new MenuFlyoutSeparator());
            AddToggleItem(menu, "作業報告に含めない", row.ReportExcluded, isChecked => ViewModel.SetReportExcludedAsync(row, isChecked), !row.IsLocked);
        }

        // 案件の並びは名前の順で、案件の登録・名前の変更・削除は案件の管理のウィンドウで行う
        if (row.IsProject)
        {
            return;
        }

        menu.Items.Add(new MenuFlyoutSeparator());
        AddItem(menu, "上へ", () => ViewModel.ShiftAsync(row, -1), ViewModel.CanShift(row, -1));
        AddItem(menu, "下へ", () => ViewModel.ShiftAsync(row, 1), ViewModel.CanShift(row, 1));
        AddItem(menu, "移動…", () => ViewModel.MoveAsync(row), !row.IsLocked);
        menu.Items.Add(new MenuFlyoutSeparator());
        AddItem(menu, "削除…", () => ViewModel.DeleteAsync(row), !row.IsLocked);
    }

    /// <summary>メニューに項目を足す</summary>
    /// <param name="menu">足し先のメニュー</param>
    /// <param name="text">項目の文言</param>
    /// <param name="action">選ばれたときの処理</param>
    /// <param name="isEnabled">選べるか</param>
    private static void AddItem(MenuFlyout menu, string text, Func<Task> action, bool isEnabled = true)
    {
        var item = new MenuFlyoutItem { Text = text, IsEnabled = isEnabled };
        item.Click += (_, _) => action().Forget();
        menu.Items.Add(item);
    }

    /// <summary>メニューに、チェック付きの項目を足す</summary>
    /// <param name="menu">足し先のメニュー</param>
    /// <param name="text">項目の文言</param>
    /// <param name="isChecked">今、チェックが付いているか</param>
    /// <param name="action">選ばれたときの処理 (選んだあとのチェックの状態を受け取る)</param>
    /// <param name="isEnabled">選べるか</param>
    private static void AddToggleItem(MenuFlyout menu, string text, bool isChecked, Func<bool, Task> action, bool isEnabled = true)
    {
        var item = new ToggleMenuFlyoutItem { Text = text, IsChecked = isChecked, IsEnabled = isEnabled };
        item.Click += (_, _) => action(item.IsChecked).Forget();
        menu.Items.Add(item);
    }

    #endregion
}
