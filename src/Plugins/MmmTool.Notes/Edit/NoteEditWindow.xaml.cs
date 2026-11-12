using System.Globalization;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using MmmSdk.Core.Components.Hosting;
using MmmSdk.WinUI.Components.DaysOfWeek;
using MmmSdk.WinUI.Components.Dialogs;
using MmmSdk.WinUI.Components.Windowing;
using MmmSdk.WinUI.Utilities;
using MmmTool.Notes.Core;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics;

namespace MmmTool.Notes.Edit;

/// <summary>ノート入力ウィンドウ</summary>
/// <remarks>
/// 親ウィンドウの上にモーダル (閉じるまで親を操作できない)で出す。
/// 大きさは変えられる。
/// 本文を広く書けるようにするため。
/// 最大化・最小化はできない。
/// タイトル帯をドラッグして移動できる。
/// × で閉じたときはキャンセルと同じ。
/// 開くたびに作り直す。
/// 閉じたウィンドウは再表示できないため。
/// 本文欄の上のボタンか右クリックで、書式 (太字・斜体・下線・取消線・色)とリンクを、HTML のタグにして足せる。
/// ファイルやフォルダーをドロップするとリンクが入る。
/// </remarks>
public sealed partial class NoteEditWindow : Window
{
    /// <summary>ウィンドウの幅で、単位は DIP</summary>
    /// <remarks>横長の 2 列にして、縦横の比を黄金比に近づける。</remarks>
    private const double WindowWidth = 1000;

    /// <summary>ウィンドウの高さで、単位は DIP</summary>
    /// <remarks>タイトルバーを含む</remarks>
    private const double WindowHeight = 640;

    /// <summary>最小の幅で、単位は DIP</summary>
    /// <remarks>
    /// 期間の行が最も広い。
    /// 右の列は内容の幅で、本文の列が縮む。
    /// 本文の列に 300 ほどを残す幅。
    /// </remarks>
    private const double MinimumWidth = 800;

    /// <summary>最小の高さで、単位は DIP</summary>
    /// <remarks>
    /// 右の列が切れない高さ。
    /// InfoBar は左の列の本文が縮んで場所を空けるので、右の列は InfoBar に左右されない。
    /// </remarks>
    private const double MinimumHeight = 560;

    /// <summary>タイトルバーの高さ</summary>
    /// <remarks>
    /// 単位は DIP。
    /// ResizeClient はタイトルバーを自分で描いていても、この分を上に足した大きさにする。
    /// </remarks>
    private const double TitleBarHeight = 32;

    /// <summary>日付の欄の表示の形で、曜日つき</summary>
    /// <remarks>PC の言語に依らず日本語</remarks>
    private const string DateFormat = "{year.full}/{month.integer(2)}/{day.integer(2)} ({dayofweek.abbreviated})";

    /// <summary>閉じたときに結果を返す</summary>
    private readonly TaskCompletionSource<bool> _closed = new();

    /// <summary>擬似モーダルの処理</summary>
    private readonly PseudoModal _modal;

    /// <summary>ダイアログの親を決めるサービス</summary>
    private readonly IDialogHost _dialogs;

    /// <summary>選んだ文字に書式を付ける項目</summary>
    /// <remarks>文字を選んでいるときだけ押せる</remarks>
    private readonly List<MenuFlyoutItemBase> _selectionItems = [];

    /// <summary>文字を選んでいるときだけ押せる書式のボタン</summary>
    private readonly List<Button> _selectionButtons = [];

    /// <summary>書式の定義</summary>
    /// <remarks>
    /// 選んだ文字をタグで囲むもの。
    /// 右クリックのメニューと本文欄の上のボタンの両方がこれから作られる。
    /// アイコンは親を 1 つしか持てないので、使うたびに作る。
    /// 取り消し線は Symbol の一覧に無いため、Segoe Fluent Icons の字形 (U+EDE0)を FontIcon で指定する。
    /// </remarks>
    private static readonly (string Text, Func<IconElement> CreateIcon, string Tag)[] Formats =
    [
        ("太字", () => new SymbolIcon(Symbol.Bold), "b"),
        ("斜体", () => new SymbolIcon(Symbol.Italic), "i"),
        ("下線", () => new SymbolIcon(Symbol.Underline), "u"),
        ("取消線", () => new FontIcon { Glyph = "" }, "s"),
    ];

    /// <summary>色のパネルの見本とその色</summary>
    /// <remarks>パネルを開くときにテーマに合わせて塗る</remarks>
    private readonly List<(Border Swatch, NoteColor Color)> _colorSwatches = [];

    /// <summary>色のパネル</summary>
    private readonly Flyout _colorFlyout;

    /// <summary>保存したか</summary>
    private bool _saved;

    /// <summary>メニューを開いたときの本文欄の選択の開始位置</summary>
    private int _selectionStart;

    /// <summary>メニューを開いたときの本文欄の選択の長さ</summary>
    private int _selectionLength;

    /// <summary>ウィンドウの ViewModel</summary>
    public NoteEditViewModel ViewModel { get; }

    /// <summary>ウィンドウを作る</summary>
    /// <param name="viewModel">ウィンドウの ViewModel</param>
    /// <param name="environment">アプリの名前と、データ・アイコンの置き場所。</param>
    /// <param name="dialogs">ダイアログの親を決めるサービス (リンクの入力に使う)</param>
    public NoteEditWindow(NoteEditViewModel viewModel, AppEnvironment environment, IDialogHost dialogs)
    {
        ViewModel = viewModel;
        _dialogs = dialogs;
        InitializeComponent();

        this.UseCustomTitleBar(TitleBarArea, environment.IconPath);
        _modal = new PseudoModal(this);

        foreach (var picker in PeriodPickers())
        {
            picker.Language = "ja-JP";
            picker.DateFormat = DateFormat;
        }
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;

        // パネルやリンクの入力に移っても、選んでいた文字が選択のまま見えるように (標準では、フォーカスを失うと選択の色が消える)
        BodyBox.SelectionHighlightColorWhenNotFocused = (SolidColorBrush)Application.Current.Resources["AccentFillColorDefaultBrush"];
        _colorFlyout = BuildColorFlyout();
        BodyBox.ContextFlyout = BuildBodyMenu();
        BuildFormatBar();
        BodyBox.SelectionChanged += (_, _) => UpdateFormatBar();
        // 文字の貼り付けとは別に、ファイル・フォルダーのドロップを受ける (欄が処理済みにしても、受ける)
        BodyBox.AddHandler(UIElement.DragOverEvent, new DragEventHandler(OnBodyDragOver), true);
        BodyBox.AddHandler(UIElement.DropEvent, new DragEventHandler(OnBodyDrop), true);

        ViewModel.CloseRequested += OnCloseRequested;
        Closed += OnClosed;
    }

    /// <summary>親ウィンドウの上にモーダルで表示し、閉じるまで待つ</summary>
    /// <param name="owner">親ウィンドウ</param>
    /// <param name="note">
    /// 編集するノート
    /// 新規なら null
    /// </param>
    /// <param name="defaultStartDate">新規のときの、開始日の初期値。</param>
    /// <returns>
    /// 保存したら true
    /// キャンセル・× なら false
    /// </returns>
    public async Task<bool> ShowModalAsync(Window owner, Note? note, DateOnly defaultStartDate)
    {
        ViewModel.Load(note, defaultStartDate);

        _modal.SetOwner(owner);
        var scale = this.GetDpiScale();
        // 画面が小さい (フル HD の 150% など)ときは、作業領域に収まる幅と高さまで抑える。最小の大きさも、その分下げる
        var width = Math.Min(WindowWidth, _modal.GetAvailableWidthDip());
        var height = Math.Min(WindowHeight, _modal.GetAvailableHeightDip());
        var minimumWidth = Math.Min(MinimumWidth, width);
        var minimumHeight = Math.Min(MinimumHeight, height);
        this.UseFixedPresenter(isDialog: true, isResizable: true, new SizeInt32((int)(minimumWidth * scale), (int)(minimumHeight * scale)));
        this.ResizeClientDip(width, height - TitleBarHeight, _modal.OwnerScale, roundUp: true);
        _modal.CenterOnOwner();

        _modal.Show();
        return await _closed.Task;
    }

    /// <summary>期間と実施日の日付の欄</summary>
    /// <returns>開始日・終了日・実施日の欄</returns>
    private CalendarDatePicker[] PeriodPickers() => [StartDatePicker, EndDatePicker, EventDatePicker];

    /// <summary>期間の外の警告が出ている間、開始日・終了日の欄の枠をエラーの色にする</summary>
    /// <param name="sender">ViewModel</param>
    /// <param name="e">変わったプロパティの情報</param>
    /// <remarks>
    /// 実施日の欄は実施日が外れているときだけ。
    /// どの欄が原因かを、警告の文と別に、欄そのものでも分かるようにする。
    /// ほかの実施日は外れた行の文字をエラーの色にする。
    /// </remarks>
    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(NoteEditViewModel.IsPeriodErrorOpen))
        {
            MarkInvalid(StartDatePicker, ViewModel.IsPeriodErrorOpen);
            MarkInvalid(EndDatePicker, ViewModel.IsPeriodErrorOpen);
        }
        else if (e.PropertyName is nameof(NoteEditViewModel.IsEventDateInvalid))
        {
            MarkInvalid(EventDatePicker, ViewModel.IsEventDateInvalid);
        }
    }

    /// <summary>日付の欄の枠をエラーの色にする・元に戻す</summary>
    /// <param name="picker">日付の欄</param>
    /// <param name="isInvalid">エラーの色にするか</param>
    private static void MarkInvalid(CalendarDatePicker picker, bool isInvalid)
    {
        if (isInvalid)
        {
            picker.BorderBrush = (Brush)Application.Current.Resources["SystemFillColorCriticalBrush"];
            picker.BorderThickness = new Thickness(2);
        }
        else
        {
            picker.ClearValue(Control.BorderBrushProperty);
            picker.ClearValue(Control.BorderThicknessProperty);
        }
    }

    /// <summary>ほかの実施日の行の文字の色</summary>
    /// <param name="isInvalid">期間の外か</param>
    /// <returns>文字の色</returns>
    /// <remarks>期間の外のときはエラーの色</remarks>
    public static Brush ForegroundOf(bool isInvalid)
        => (Brush)Application.Current.Resources[isInvalid ? "SystemFillColorCriticalBrush" : "TextFillColorPrimaryBrush"];

    /// <summary>「実施日を追加」を押したらカレンダーを開く</summary>
    /// <param name="sender">ボタン</param>
    /// <param name="e">イベントの情報</param>
    /// <remarks>
    /// 日を押すたびに入る・外れるが切り替わり、「閉じる」まで開いたままにする。
    /// 開くたびに作り直す。
    /// 実施日の日を押せない日にする印を開くときの実施日に合わせるため。
    /// 開いたときに見せる月はいつも今日の月。
    /// これから足す日を選ぶカレンダーで、既にある日の月を見せないため。
    /// 土曜・日曜・祝日の線は日付の欄のカレンダーと同じ。
    /// 開いただけでは何も入れない。
    /// </remarks>
    private void OnAddOtherEventDateClick(object sender, RoutedEventArgs e)
    {
        var calendar = new CalendarView
        {
            SelectionMode = CalendarViewSelectionMode.Multiple,
            Language = "ja-JP",
        };
        foreach (var item in ViewModel.OtherEventDates)
        {
            calendar.SelectedDates.Add(new DateTimeOffset(item.Date.ToDateTime(TimeOnly.MinValue)));
        }
        calendar.SetDisplayDate(DateTimeOffset.Now);

        // 土曜は青、日曜と祝日は赤の線を、日の下に付ける。日付の欄のカレンダーと同じ色
        CalendarHolidayColoring.SetIsEnabled(calendar, true);

        // 実施日の日は、同じ日を入れられないので、押せない日にする
        var eventDay = ViewModel.EventDate?.Date;
        calendar.CalendarViewDayItemChanging += (_, args) => args.Item.IsBlackout = eventDay is { } day && args.Item.Date.Date == day;
        calendar.SelectedDatesChanged += (_, args) =>
        {
            foreach (var added in args.AddedDates)
            {
                ViewModel.AddOtherEventDate(DateOnly.FromDateTime(added.Date));
            }
            foreach (var removed in args.RemovedDates)
            {
                ViewModel.RemoveOtherEventDate(DateOnly.FromDateTime(removed.Date));
            }
        };

        // 複数選べることを、カレンダーの上の 1 行で伝える。終わらせるボタンは「完了」で、強調色にする (日を押した時点で入っているので、「閉じる」ではキャンセルに見える)
        var hint = new TextBlock
        {
            Text = "日を押して、何日でも追加できます。もう一度押すと外れます。",
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 296,
            Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
        };
        var flyout = new Flyout();
        var done = new Button
        {
            Content = "完了",
            HorizontalAlignment = HorizontalAlignment.Right,
            Style = (Style)Application.Current.Resources["AccentButtonStyle"],
        };
        done.Click += (_, _) => flyout.Hide();
        flyout.Content = new StackPanel { Spacing = 8, Children = { hint, calendar, done } };
        flyout.ShowAt(AddOtherDateButton, new FlyoutShowOptions { Placement = FlyoutPlacementMode.Bottom });
    }

    /// <summary>タイトル・本文にフォーカスが来たら IME をオンにする</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">イベントの情報</param>
    /// <remarks>日本語の入力が多いため</remarks>
    private void OnJapaneseBoxGotFocus(object sender, RoutedEventArgs e) => ImeControl.TurnOn();

    /// <summary>本文欄の右クリックのメニューを作る</summary>
    /// <returns>メニュー</returns>
    /// <remarks>
    /// 文字を選んでいるときだけ、書式と色が押せる。
    /// 「リンクを追加」は選んでいなくても押せる。
    /// 切り取り・コピー・貼り付け・すべて選択も入れる。
    /// 欄の標準のメニューを置き換えるため。
    /// </remarks>
    private MenuFlyout BuildBodyMenu()
    {
        var menu = new MenuFlyout();

        // 文字の装飾 (このメニューの主役)を、よく使う順に。色は、重要な連絡を目立たせるのに使うので、先頭に置く
        menu.Items.Add(BuildColorItem());
        foreach (var (text, createIcon, tag) in Formats)
        {
            AddFormat(menu, text, createIcon(), tag);
        }

        menu.Items.Add(new MenuFlyoutSeparator());
        var link = new MenuFlyoutItem { Text = "リンクを追加", Icon = new SymbolIcon(Symbol.Link) };
        link.Click += OnAddLinkClick;
        menu.Items.Add(link);

        menu.Items.Add(new MenuFlyoutSeparator());
        AddClipboard(menu, "切り取り", Symbol.Cut, () => BodyBox.CutSelectionToClipboard(), needsSelection: true);
        AddClipboard(menu, "コピー", Symbol.Copy, () => BodyBox.CopySelectionToClipboard(), needsSelection: true);
        AddClipboard(menu, "貼り付け", Symbol.Paste, () => BodyBox.PasteFromClipboard(), needsSelection: false);
        AddClipboard(menu, "すべて選択", Symbol.SelectAll, () => BodyBox.SelectAll(), needsSelection: false);

        menu.Opening += OnBodyMenuOpening;
        return menu;
    }

    /// <summary>本文欄の上に書式のボタンの列を作る</summary>
    /// <remarks>
    /// 並びと名前は右クリックのメニューと同じ
    /// 色・太字・斜体・下線・取消線・リンクを追加の順に並べる
    /// 右クリックを知らなくても書式を付けられるようにするため
    /// 名前はツールチップに出す
    /// </remarks>
    private void BuildFormatBar()
    {
        var color = CreateFormatButton("色", new SymbolIcon(Symbol.FontColor), needsSelection: true);
        color.Click += (_, _) =>
        {
            CaptureSelection();
            _colorFlyout.ShowAt(color, new FlyoutShowOptions { Placement = FlyoutPlacementMode.Bottom });
        };

        foreach (var (text, createIcon, tag) in Formats)
        {
            var button = CreateFormatButton(text, createIcon(), needsSelection: true);
            button.Click += (_, _) =>
            {
                CaptureSelection();
                ReplaceSelection(selected => NoteMarkup.Wrap(selected, tag));
            };
        }

        FormatBar.Children.Add(new Border
        {
            Width = 1,
            Height = 20,
            Margin = new Thickness(4, 0, 4, 0),
            Background = (Brush)Application.Current.Resources["DividerStrokeColorDefaultBrush"],
        });

        var link = CreateFormatButton("リンクを追加", new SymbolIcon(Symbol.Link), needsSelection: false);
        link.Click += (sender, e) =>
        {
            CaptureSelection();
            OnAddLinkClick(sender, e);
        };

        UpdateFormatBar();
    }

    /// <summary>書式のボタンを 1 つ作り、列に足す</summary>
    /// <param name="text">名前 (ツールチップと、読み上げに使う)。</param>
    /// <param name="icon">アイコン</param>
    /// <param name="needsSelection">文字を選んでいるときだけ押せるか</param>
    /// <returns>足したボタン</returns>
    private Button CreateFormatButton(string text, IconElement icon, bool needsSelection)
    {
        var button = new Button
        {
            Content = icon,
            Width = 32,
            Height = 32,
            Padding = new Thickness(0),
            Background = new SolidColorBrush(Colors.Transparent),
            BorderThickness = new Thickness(0),
        };
        ToolTipService.SetToolTip(button, text);
        AutomationProperties.SetName(button, text);
        FormatBar.Children.Add(button);
        if (needsSelection)
        {
            _selectionButtons.Add(button);
        }

        return button;
    }

    /// <summary>文字を選んでいるときだけ書式のボタンを押せるようにする</summary>
    private void UpdateFormatBar()
    {
        foreach (var button in _selectionButtons)
        {
            button.IsEnabled = BodyBox.SelectionLength > 0;
        }
    }

    /// <summary>本文欄で選んでいる文字を控える</summary>
    /// <remarks>ボタンを押すとフォーカスが移るため、押した時点の選択を使う。</remarks>
    private void CaptureSelection()
    {
        _selectionStart = BodyBox.SelectionStart;
        _selectionLength = BodyBox.SelectionLength;
    }

    /// <summary>書式の項目を足す</summary>
    /// <param name="menu">足し先のメニュー</param>
    /// <param name="text">項目の文字</param>
    /// <param name="icon">
    /// 項目のアイコン
    /// 無ければ null
    /// </param>
    /// <param name="tag">囲むタグの名前</param>
    /// <remarks>選んだ文字をタグで囲む</remarks>
    private void AddFormat(MenuFlyout menu, string text, IconElement? icon, string tag)
    {
        var item = new MenuFlyoutItem { Text = text, Icon = icon };
        item.Click += (_, _) => ReplaceSelection(selected => NoteMarkup.Wrap(selected, tag));
        menu.Items.Add(item);
        _selectionItems.Add(item);
    }

    /// <summary>切り取り・コピー・貼り付け・すべて選択の項目を足す</summary>
    /// <param name="menu">足し先のメニュー</param>
    /// <param name="text">項目の文字</param>
    /// <param name="symbol">アイコン</param>
    /// <param name="action">行う処理</param>
    /// <param name="needsSelection">文字を選んでいるときだけ押せるか</param>
    private void AddClipboard(MenuFlyout menu, string text, Symbol symbol, Action action, bool needsSelection)
    {
        var item = new MenuFlyoutItem { Text = text, Icon = new SymbolIcon(symbol) };
        item.Click += (_, _) =>
        {
            BodyBox.Focus(FocusState.Programmatic);
            BodyBox.Select(_selectionStart, _selectionLength);
            action();
        };
        menu.Items.Add(item);
        if (needsSelection)
        {
            _selectionItems.Add(item);
        }
    }

    /// <summary>「色」の項目を作る</summary>
    /// <returns>メニューの項目</returns>
    /// <remarks>
    /// 押すと色のパネルが開く。
    /// メニューの中に、さらに入れ子のメニューを開くと、色を選ぶのに何度も辿ることになる。
    /// そのため、色は 1 枚のパネルで選ぶ。
    /// </remarks>
    private MenuFlyoutItem BuildColorItem()
    {
        var item = new MenuFlyoutItem { Text = "色", Icon = new SymbolIcon(Symbol.FontColor) };
        item.Click += (_, _) => DispatcherQueue.TryEnqueue(ShowColorFlyout);
        _selectionItems.Add(item);
        return item;
    }

    /// <summary>色のパネルを選んでいる文字の直下に出す</summary>
    /// <remarks>欄の見えている範囲の外に文字があるとき (スクロールしているとき)は範囲の端に寄せる</remarks>
    private void ShowColorFlyout()
    {
        var rect = BodyBox.GetRectFromCharacterIndex(_selectionStart, false);
        var position = new Windows.Foundation.Point(
            Math.Clamp(rect.X, 0, BodyBox.ActualWidth),
            Math.Clamp(rect.Bottom, 0, BodyBox.ActualHeight));
        _colorFlyout.ShowAt(BodyBox, new FlyoutShowOptions { Position = position, Placement = FlyoutPlacementMode.Bottom });
    }

    /// <summary>色のパネルを作る</summary>
    /// <returns>パネル</returns>
    private Flyout BuildColorFlyout()
    {
        var standard = new Button { Content = "標準", HorizontalAlignment = HorizontalAlignment.Stretch };
        standard.Click += (_, _) => PickColor(NoteMarkup.RemoveColor);

        var grid = new Grid { RowSpacing = 4, ColumnSpacing = 4 };
        var colors = NoteColorPalette.All;
        var tones = NoteColorPalette.ToneCount;
        for (var tone = 0; tone < tones; tone++)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }
        for (var family = 0; family < colors.Count / tones; family++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            for (var tone = 0; tone < tones; tone++)
            {
                var color = colors[family * tones + tone];
                var swatch = new Border { Width = 20, Height = 20, CornerRadius = new CornerRadius(3) };
                var button = new Button { Content = swatch, Padding = new Thickness(4), MinWidth = 0, MinHeight = 0 };
                ToolTipService.SetToolTip(button, color.Name);
                AutomationProperties.SetName(button, color.Name);
                button.Click += (_, _) => PickColor(selected => NoteMarkup.WrapColor(selected, color));
                Grid.SetColumn(button, family);
                Grid.SetRow(button, tone);
                grid.Children.Add(button);
                _colorSwatches.Add((swatch, color));
            }
        }

        var flyout = new Flyout { Content = new StackPanel { Spacing = 8, Children = { standard, grid } } };
        flyout.Opening += OnColorFlyoutOpening;
        return flyout;
    }

    /// <summary>色のパネルを開くとき、色の見本を今のテーマで読める色に塗る</summary>
    /// <param name="sender">パネル</param>
    /// <param name="e">イベントの情報</param>
    /// <remarks>
    /// 本文に入る色はライトの値
    /// ダークでは表示のときに同じ系統の別の色に差し替わる
    /// </remarks>
    private void OnColorFlyoutOpening(object? sender, object e)
    {
        var dark = RootGrid.ActualTheme == ElementTheme.Dark;
        foreach (var (swatch, color) in _colorSwatches)
        {
            swatch.Background = new SolidColorBrush(ParseColor(dark ? color.DarkHex : color.Hex));
        }
    }

    /// <summary>パネルで色を選んだら、パネルを閉じて、選んでいた文字に反映する</summary>
    /// <param name="transform">選んでいた文字から、置き換える文字を作る処理。</param>
    private void PickColor(Func<string, string> transform)
    {
        _colorFlyout.Hide();
        ReplaceSelection(transform);
    }

    /// <summary>メニューを開くとき、選んでいる文字を控え、押せる項目と色の見本を合わせる</summary>
    /// <param name="sender">メニュー</param>
    /// <param name="e">イベントの情報</param>
    private void OnBodyMenuOpening(object? sender, object e)
    {
        CaptureSelection();

        foreach (var item in _selectionItems)
        {
            item.IsEnabled = _selectionLength > 0;
        }

    }

    /// <summary>メニューを開いたときに選んでいた文字を変換した文字に置き換える</summary>
    /// <param name="transform">選んでいた文字から、置き換える文字を作る処理。</param>
    private void ReplaceSelection(Func<string, string> transform)
    {
        BodyBox.Focus(FocusState.Programmatic);
        BodyBox.Select(_selectionStart, _selectionLength);
        var replaced = transform(BodyBox.SelectedText);
        BodyBox.SelectedText = replaced;
        BodyBox.Select(_selectionStart, replaced.Length);
    }

    /// <summary>「リンクを追加」を押したら、リンク名とリンク先を聞いて、リンクを入れる</summary>
    /// <param name="sender">メニューの項目</param>
    /// <param name="e">イベントの情報</param>
    /// <remarks>文字を選んでいるときはその文字がリンク名の初期値になる</remarks>
    private async void OnAddLinkClick(object sender, RoutedEventArgs e)
    {
        BodyBox.Select(_selectionStart, _selectionLength);
        var asked = await AskLinkAsync(BodyBox.SelectedText);
        if (asked is not { } link)
        {
            return;
        }

        BodyBox.Focus(FocusState.Programmatic);
        BodyBox.Select(_selectionStart, _selectionLength);
        BodyBox.SelectedText = NoteMarkup.Link(link.Name, link.Target);
    }

    /// <summary>リンク名とリンク先を聞く</summary>
    /// <param name="initialName">リンク名の初期値</param>
    /// <returns>
    /// 入力した内容
    /// キャンセルなら null
    /// </returns>
    private async Task<(string Name, string Target)?> AskLinkAsync(string initialName)
    {
        var nameBox = new TextBox { Header = "リンク名", Text = initialName, PlaceholderText = "省略すると、リンク先を表示します" };
        var targetBox = new TextBox { Header = "リンク先", PlaceholderText = "URL またはファイル・フォルダーのパス" };
        nameBox.GotFocus += (_, _) => ImeControl.TurnOn();
        targetBox.GotFocus += (_, _) => ImeControl.TurnOff();

        var dialog = new ContentDialog
        {
            Title = "リンクを追加",
            Content = new StackPanel { Spacing = 12, MinWidth = 420, Children = { nameBox, targetBox } },
            PrimaryButtonText = "追加",
            CloseButtonText = "キャンセル",
            DefaultButton = ContentDialogButton.Primary,
            IsPrimaryButtonEnabled = false,
        };
        targetBox.TextChanged += (_, _) => dialog.IsPrimaryButtonEnabled = targetBox.Text.Trim().Length > 0;

        _dialogs.Attach(dialog);
        return await dialog.ShowAsync() == ContentDialogResult.Primary ? (nameBox.Text, targetBox.Text) : null;
    }

    /// <summary>ファイル・フォルダーを本文欄の上に持ってきたら、落とせるようにする</summary>
    /// <param name="sender">本文欄</param>
    /// <param name="e">ドラッグの情報</param>
    private void OnBodyDragOver(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
            e.DragUIOverride.Caption = "リンクを追加";
            e.Handled = true;
        }
    }

    /// <summary>ファイル・フォルダーを落としたら、カーソルの位置にリンクを入れる</summary>
    /// <param name="sender">本文欄</param>
    /// <param name="e">ドロップの情報</param>
    /// <remarks>
    /// リンク名はファイル・フォルダーの名前
    /// 複数を落としたときは 1 つずつ改行して並べる
    /// </remarks>
    private async void OnBodyDrop(object sender, DragEventArgs e)
    {
        if (!e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            return;
        }

        var deferral = e.GetDeferral();
        try
        {
            var items = await e.DataView.GetStorageItemsAsync();
            var paths = items.Select(item => item.Path).Where(path => !string.IsNullOrEmpty(path)).ToList();
            if (paths.Count > 0)
            {
                BodyBox.SelectedText = NoteMarkup.LinksFromPaths(paths);
                BodyBox.SelectionStart += BodyBox.SelectionLength;
                BodyBox.SelectionLength = 0;
            }
            e.Handled = true;
        }
        finally
        {
            deferral.Complete();
        }
    }

    /// <summary>#RRGGBB の文字から色にする</summary>
    /// <param name="hex">色の値</param>
    /// <returns>色</returns>
    private static Windows.UI.Color ParseColor(string hex)
    {
        var value = int.Parse(hex.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return ColorHelper.FromArgb(255, (byte)(value >> 16 & 0xFF), (byte)(value >> 8 & 0xFF), (byte)(value & 0xFF));
    }

    /// <summary>保存・キャンセルで閉じる</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="saved">保存したか</param>
    private void OnCloseRequested(object? sender, bool saved)
    {
        _saved = saved;
        _modal.Close();
    }

    /// <summary>閉じたら結果を返す</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="args">閉じたイベントの情報</param>
    private void OnClosed(object sender, WindowEventArgs args)
    {
        ViewModel.CloseRequested -= OnCloseRequested;
        ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _closed.TrySetResult(_saved);
    }
}
