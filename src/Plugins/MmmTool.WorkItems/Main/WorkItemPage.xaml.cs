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
using MmmTool.WorkItems.Core;
using Windows.System;

namespace MmmTool.WorkItems.Main;

/// <summary>作業リストのページ</summary>
/// <remarks>
/// 表 (<see cref="TreeGridView"/>)の列と右クリックのメニューを組み立て、セルの入力の確定を ViewModel に渡す。
/// 入力欄は表の行を使い回すので、確定の操作 (フォーカスが外れたとき・Enter)だけを保存に結び付け、値の変更のイベント (選択・日付の変更)には結び付けない
/// (使い回しの値の差し替えで、保存が走らないようにするため)。
/// </remarks>
public sealed partial class WorkItemPage : Page
{
    /// <summary>列の定義 (キー・見出し・幅・固定か・毎日入力する列か)</summary>
    private readonly List<TreeGridColumn> _columns;

    /// <summary>入力日の実績の列</summary>
    private readonly TreeGridColumn _actualDayColumn;

    /// <summary>入力日の備考の列</summary>
    private readonly TreeGridColumn _dayNoteColumn;

    /// <summary>毎日入力する列のキー (見出しの色を付ける)</summary>
    private static readonly HashSet<string> DailyKeys = ["Status", "Progress", "ActualDay", "DayNote", "Note"];

    /// <summary>ページの ViewModel</summary>
    public WorkItemViewModel ViewModel { get; }

    /// <summary>ページを作る</summary>
    /// <param name="viewModel">ページの ViewModel</param>
    public WorkItemPage(WorkItemViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();

        _actualDayColumn = Column("ActualDay", "実績 (日)", 100, "ActualDayCell");
        _dayNoteColumn = Column("DayNote", "備考 (日)", 220, "DayNoteCell");
        _columns =
        [
            Column("Name", "名前", 260, "NameCell", frozen: true, tree: true),
            Column("Priority", "優先度", 72, "PriorityCell", frozen: true),
            Column("Start", "開始日", 132, "StartCell", frozen: true),
            Column("Due", "期限日", 132, "DueCell", frozen: true),
            Column("Planned", "予定 (累計)", 84, "PlannedCell", frozen: true),
            Column("ActualTotal", "実績 (累計)", 96, "ActualTotalCell", frozen: true),
            Column("Status", "状態", 104, "StatusCell"),
            Column("Progress", "進捗度", 190, "ProgressCell"),
            _actualDayColumn,
            _dayNoteColumn,
            Column("Note", "備考", 260, "NoteCell"),
        ];
        _columns.First(c => c.Key == "Planned").HeaderToolTip = "作業全体の予定の工数 (時間)";
        foreach (var column in _columns)
        {
            TreeGrid.Columns.Add(column);
        }

        InputDatePicker.DateFormat = "{year.full}/{month.integer(2)}/{day.integer(2)}";
        InputDatePicker.Date = ToOffset(ViewModel.InputDate);

        var rowMenu = (MenuFlyout)Resources["RowMenu"];
        var blankMenu = (MenuFlyout)Resources["BlankMenu"];
        rowMenu.Opening += OnRowMenuOpening;
        blankMenu.Opening += OnBlankMenuOpening;
        TreeGrid.RowContextFlyout = rowMenu;
        TreeGrid.BlankContextFlyout = blankMenu;
        TreeGrid.CellInvoked += OnCellInvoked;
        TreeGrid.ItemsSource = ViewModel.Rows;
        TreeGrid.RowToggleRequested += (_, e) => ViewModel.ToggleAsync((WorkItemRow)e.Row).Forget();
        TreeGrid.SelectedRowChanged += (_, _) => ViewModel.SelectedRow = TreeGrid.SelectedRow as WorkItemRow;

        BuildColumnsFlyout();
        ApplyColumns();

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
        ActualThemeChanged += (_, _) => ApplyColumns();
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
            IsInvokable = key is "Priority" or "Status" or "Progress",
        };

    /// <summary>表示用のセルの見た目のキーから、入力用のキーを返す</summary>
    /// <param name="templateKey">表示用のキー (…Cell)</param>
    /// <returns>入力用のキー (…Edit)</returns>
    private static string EditKey(string templateKey) => templateKey[..^"Cell".Length] + "Edit";

    /// <summary>設定と入力日に合わせて、列の表示・見出し・色を直し、表に反映する</summary>
    private void ApplyColumns()
    {
        var lineBrush = (Brush)Resources["DailyLineBrush"];
        foreach (var column in _columns)
        {
            column.IsVisible = column.Key == "Name" || ViewModel.IsColumnVisible(column.Key);
            column.HeaderUnderline = DailyKeys.Contains(column.Key) ? lineBrush : null;
        }

        FitColumns();
        TreeGrid.RefreshColumns();
    }

    /// <summary>列の幅を、中身に合わせる</summary>
    /// <returns>幅が変わった列があるか</returns>
    /// <remarks>
    /// 見出し・選択肢・今の行の文字のうち、いちばん長いものに、余白を足した幅にする (備考だけは、上限を設ける)。
    /// 表示を変える操作 (行の追加・入力の確定)のあとに呼ぶ。
    /// </remarks>
    private bool FitColumns()
    {
        var rows = ViewModel.Rows;
        var progress = rows.FirstOrDefault()?.ProgressOptions ?? [];
        var changed = false;
        foreach (var column in _columns)
        {
            var width = column.Key switch
            {
                "Name" => Math.Max(HeaderWidth(column), rows.Select(r => 4 + (r.Level * TreeGrid.IndentWidth) + 22 + TextWidth(r.Name, WorkItemOptions.NameWeight(r.IsGroup, r.Level), r.IsProject ? WorkItemOptions.ProjectFontSize : 14) + 40).DefaultIfEmpty(0).Max()),
                "Priority" => Math.Max(HeaderWidth(column), WidestWidth(WorkItemOptions.Priorities) + 48),
                "Start" or "Due" => Math.Max(HeaderWidth(column), TextWidth("2026/10/10", Microsoft.UI.Text.FontWeights.Normal) + 52),
                "Planned" => Math.Max(HeaderWidth(column), TextWidth("時間 (H)", Microsoft.UI.Text.FontWeights.Normal) + 24),
                "ActualTotal" => Math.Max(HeaderWidth(column), TextWidth("0000.00", Microsoft.UI.Text.FontWeights.Normal) + 24),
                "Status" => Math.Max(HeaderWidth(column), WidestWidth(WorkItemOptions.Statuses) + 48),
                "Progress" => Math.Max(HeaderWidth(column), WidestWidth(progress) + 48),
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
        foreach (var column in _columns.Where(c => c.Key != "Name"))
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
            // 日付を消されたときは、今の入力日に戻す (入力日は、いつも決まっている)
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
                ApplyColumns();
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
    /// 日付を消すときは、Delete キーか Backspace キーを押す (<see cref="OnDateKeyDown"/>)。
    /// </remarks>
    private void OnCellDateChanged(CalendarDatePicker sender, CalendarDatePickerDateChangedEventArgs args)
    {
        if (TreeGridView.GetRow(sender) is WorkItemRow row)
        {
            var text = args.NewDate is { } date ? date.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture) : "";
            ViewModel.CommitTextAsync(row, ColumnKey(sender), text).Forget();
        }
    }

    /// <summary>日付の選択欄で Delete キーか Backspace キーが押されたら、日付を消す</summary>
    /// <param name="sender">日付の選択欄</param>
    /// <param name="e">キー入力の情報</param>
    private void OnDateKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (sender is CalendarDatePicker picker && e.Key is VirtualKey.Delete or VirtualKey.Back)
        {
            e.Handled = true;
            picker.Date = null;
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
            ViewModel.CommitTextAsync(row, ColumnKey(box), box.Text).Forget();
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

        var (options, current) = e.ColumnKey switch
        {
            "Priority" => (WorkItemOptions.Priorities, row.PriorityIndex),
            "Status" => (WorkItemOptions.Statuses, row.StatusIndex),
            "Progress" => (row.ProgressOptions, row.ProgressIndex),
            _ => ((IReadOnlyList<string>)[], -1),
        };

        var menu = new MenuFlyout();
        for (var i = 0; i < options.Count; i++)
        {
            var index = i;
            var item = new RadioMenuFlyoutItem { Text = options[i], GroupName = e.ColumnKey, IsChecked = i == current };
            item.Click += (_, _) => ViewModel.CommitChoiceAsync(row, e.ColumnKey, index).Forget();
            menu.Items.Add(item);
        }
        menu.ShowAt(e.Cell, new FlyoutShowOptions { Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft });
    }

    /// <summary>備考の入力欄を開くとき、今の備考を入れる</summary>
    /// <param name="sender">開く入れ物</param>
    /// <param name="e">イベントの情報</param>
    private void OnNoteFlyoutOpening(object? sender, object e)
    {
        if (sender is Flyout { Target: Button button, Content: TextBox box } && TreeGridView.GetRow(button) is WorkItemRow row)
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
            AddItem(menu, "グループを追加", () => ViewModel.AddAsync(WorkItemKind.Group, row));
        }
        AddItem(menu, "作業を追加", () => ViewModel.AddAsync(WorkItemKind.Work, row));

        menu.Items.Add(new MenuFlyoutSeparator());
        AddItem(menu, "上へ", () => ViewModel.ShiftAsync(row, -1), ViewModel.CanShift(row, -1));
        AddItem(menu, "下へ", () => ViewModel.ShiftAsync(row, 1), ViewModel.CanShift(row, 1));
        AddItem(menu, "移動…", () => ViewModel.MoveAsync(row));
        menu.Items.Add(new MenuFlyoutSeparator());
        AddItem(menu, "削除…", () => ViewModel.DeleteAsync(row));
    }

    /// <summary>行のない所の右クリックのメニューを作る</summary>
    /// <param name="sender">メニュー</param>
    /// <param name="e">イベントの情報</param>
    private void OnBlankMenuOpening(object? sender, object e)
    {
        if (sender is not MenuFlyout menu)
        {
            return;
        }

        menu.Items.Clear();
        AddItem(menu, "案件を追加", () => ViewModel.AddAsync(WorkItemKind.Group, null));
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

    #endregion
}
