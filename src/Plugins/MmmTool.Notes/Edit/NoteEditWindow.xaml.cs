using System.Globalization;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using MmmSdk.Core.Components.Hosting;
using MmmSdk.WinUI.Components.Dialogs;
using MmmSdk.WinUI.Components.Windowing;
using MmmSdk.WinUI.Utilities;
using MmmTool.Notes.Core;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics;

namespace MmmTool.Notes.Edit;

/// <summary>ノート入力ウィンドウ (1 件の新規登録・編集)</summary>
/// <remarks>
/// 親ウィンドウの上に、モーダル (閉じるまで親を操作できない)で出す。大きさは変えられる (本文を広く書けるように)。最大化・最小化はできない。
/// タイトル帯をドラッグして移動できる。× で閉じたときはキャンセルと同じ。開くたびに作り直す (閉じたウィンドウは再表示できないため)。
/// 本文欄の右クリックで、書式 (太字・斜体・下線・取消線・色)とリンクを、HTML のタグにして足せる。ファイルやフォルダーをドロップすると、リンクが入る。
/// </remarks>
public sealed partial class NoteEditWindow : Window
{
    /// <summary>ウィンドウの幅 (DIP)</summary>
    private const double WindowWidth = 640;

    /// <summary>ウィンドウの高さ (DIP。タイトルバーを含む)</summary>
    private const double WindowHeight = 720;

    /// <summary>最小の幅 (DIP)</summary>
    private const double MinimumWidth = 520;

    /// <summary>最小の高さ (DIP)</summary>
    private const double MinimumHeight = 650;

    /// <summary>タイトルバーの高さ (DIP)</summary>
    /// <remarks>ResizeClient は、タイトルバーを自分で描いていても、この分を上に足した大きさにする。</remarks>
    private const double TitleBarHeight = 32;

    /// <summary>日付の欄の表示の形 (曜日つき。PC の言語に依らず日本語)</summary>
    private const string DateFormat = "{year.full}/{month.integer(2)}/{day.integer(2)} ({dayofweek.abbreviated})";

    /// <summary>閉じたときに結果を返す</summary>
    private readonly TaskCompletionSource<bool> _closed = new();

    /// <summary>擬似モーダルの処理</summary>
    private readonly PseudoModal _modal;

    /// <summary>ダイアログの親を決めるサービス</summary>
    private readonly IDialogHost _dialogs;

    /// <summary>選んだ文字に書式を付ける項目 (文字を選んでいるときだけ押せる)</summary>
    private readonly List<MenuFlyoutItemBase> _selectionItems = [];

    /// <summary>色のパネルの見本と、その色 (パネルを開くときに、テーマに合わせて塗る)</summary>
    private readonly List<(Border Swatch, NoteColor Color)> _colorSwatches = [];

    /// <summary>色のパネル</summary>
    private readonly Flyout _colorFlyout;

    /// <summary>保存したか</summary>
    private bool _saved;

    /// <summary>メニューを開いたときの、本文欄の選択の開始位置</summary>
    private int _selectionStart;

    /// <summary>メニューを開いたときの、本文欄の選択の長さ</summary>
    private int _selectionLength;

    /// <summary>ウィンドウの ViewModel</summary>
    public NoteEditViewModel ViewModel { get; }

    /// <summary>ウィンドウを作る</summary>
    /// <param name="viewModel">ウィンドウの ViewModel</param>
    /// <param name="environment">アプリの名前と、データ・アイコンの置き場所</param>
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
        // 文字の貼り付けとは別に、ファイル・フォルダーのドロップを受ける (欄が処理済みにしても、受ける)
        BodyBox.AddHandler(UIElement.DragOverEvent, new DragEventHandler(OnBodyDragOver), true);
        BodyBox.AddHandler(UIElement.DropEvent, new DragEventHandler(OnBodyDrop), true);

        ViewModel.CloseRequested += OnCloseRequested;
        Closed += OnClosed;
    }

    /// <summary>親ウィンドウの上にモーダルで表示し、閉じるまで待つ</summary>
    /// <param name="owner">親ウィンドウ</param>
    /// <param name="note">編集するノート。新規なら null</param>
    /// <param name="defaultStartDate">新規のときの、開始日の初期値</param>
    /// <returns>保存したら true。キャンセル・× なら false</returns>
    public async Task<bool> ShowModalAsync(Window owner, Note? note, DateOnly defaultStartDate)
    {
        ViewModel.Load(note, defaultStartDate);

        _modal.SetOwner(owner);
        var scale = this.GetDpiScale();
        // 画面が小さい (フル HD の 125% など)ときは、作業領域に収まる高さまで抑える。最小の高さも、その分下げる
        var height = Math.Min(WindowHeight, _modal.GetAvailableHeightDip());
        var minimumHeight = Math.Min(MinimumHeight, height);
        this.UseFixedPresenter(isDialog: true, isResizable: true, new SizeInt32((int)(MinimumWidth * scale), (int)(minimumHeight * scale)));
        this.ResizeClientDip(WindowWidth, height - TitleBarHeight, _modal.OwnerScale, roundUp: true);
        _modal.CenterOnOwner();

        _modal.Show();
        return await _closed.Task;
    }

    /// <summary>期間と実施日の日付の欄</summary>
    /// <returns>開始日・終了日・実施日の欄</returns>
    private CalendarDatePicker[] PeriodPickers() => [StartDatePicker, EndDatePicker, EventDatePicker];

    /// <summary>期間の外の警告が出ている間、3 つの日付の欄の枠を、エラーの色にする</summary>
    /// <param name="sender">ViewModel</param>
    /// <param name="e">変わったプロパティの情報</param>
    /// <remarks>どの欄が原因かを、警告の文と別に、欄そのものでも分かるようにする。</remarks>
    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(NoteEditViewModel.IsPeriodErrorOpen))
        {
            return;
        }

        foreach (var picker in PeriodPickers())
        {
            if (ViewModel.IsPeriodErrorOpen)
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
    }

    /// <summary>タイトル・本文にフォーカスが来たら IME をオンにする (日本語の入力が多いため)</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">イベントの情報</param>
    private void OnJapaneseBoxGotFocus(object sender, RoutedEventArgs e) => ImeControl.TurnOn();

    /// <summary>本文欄の右クリックのメニューを作る</summary>
    /// <returns>メニュー</returns>
    /// <remarks>文字を選んでいるときだけ、書式と色が押せる。「リンクを追加」は、選んでいなくても押せる。切り取り・コピー・貼り付け・すべて選択も入れる (欄の標準のメニューを置き換えるため)。</remarks>
    private MenuFlyout BuildBodyMenu()
    {
        var menu = new MenuFlyout();

        // 文字の装飾 (このメニューの主役)を、よく使う順に。色は、重要な連絡を目立たせるのに使うので、先頭に置く
        menu.Items.Add(BuildColorItem());
        AddFormat(menu, "太字", new SymbolIcon(Symbol.Bold), "b");
        AddFormat(menu, "斜体", new SymbolIcon(Symbol.Italic), "i");
        AddFormat(menu, "下線", new SymbolIcon(Symbol.Underline), "u");
        AddFormat(menu, "取消線", null, "s");

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

    /// <summary>書式の項目を足す (選んだ文字を、タグで囲む)</summary>
    /// <param name="menu">足し先のメニュー</param>
    /// <param name="text">項目の文字</param>
    /// <param name="icon">項目のアイコン。無ければ null</param>
    /// <param name="tag">囲むタグの名前</param>
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

    /// <summary>「色」の項目を作る (押すと、色のパネルが開く)</summary>
    /// <returns>メニューの項目</returns>
    /// <remarks>メニューの中に、さらに入れ子のメニューを開くと、色を選ぶのに何度も辿ることになる。そのため、色は 1 枚のパネルで選ぶ。</remarks>
    private MenuFlyoutItem BuildColorItem()
    {
        var item = new MenuFlyoutItem { Text = "色", Icon = new SymbolIcon(Symbol.FontColor) };
        item.Click += (_, _) => DispatcherQueue.TryEnqueue(ShowColorFlyout);
        _selectionItems.Add(item);
        return item;
    }

    /// <summary>色のパネルを、選んでいる文字の直下に出す</summary>
    /// <remarks>欄の見えている範囲の外に文字があるとき (スクロールしているとき)は、範囲の端に寄せる。</remarks>
    private void ShowColorFlyout()
    {
        var rect = BodyBox.GetRectFromCharacterIndex(_selectionStart, false);
        var position = new Windows.Foundation.Point(
            Math.Clamp(rect.X, 0, BodyBox.ActualWidth),
            Math.Clamp(rect.Bottom, 0, BodyBox.ActualHeight));
        _colorFlyout.ShowAt(BodyBox, new FlyoutShowOptions { Position = position, Placement = FlyoutPlacementMode.Bottom });
    }

    /// <summary>色のパネルを作る (「標準」と、系統を横・段階を縦に並べた 11 × 4 の色)</summary>
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

    /// <summary>色のパネルを開くとき、色の見本を、今のテーマ (ライト・ダーク)で読める色に塗る</summary>
    /// <param name="sender">パネル</param>
    /// <param name="e">イベントの情報</param>
    /// <remarks>本文に入る色は、ライトの値。ダークでは、表示のときに同じ系統の別の色に差し替わる。</remarks>
    private void OnColorFlyoutOpening(object? sender, object e)
    {
        var dark = RootGrid.ActualTheme == ElementTheme.Dark;
        foreach (var (swatch, color) in _colorSwatches)
        {
            swatch.Background = new SolidColorBrush(ParseColor(dark ? color.DarkHex : color.Hex));
        }
    }

    /// <summary>パネルで色を選んだら、パネルを閉じて、選んでいた文字に反映する</summary>
    /// <param name="transform">選んでいた文字から、置き換える文字を作る処理</param>
    private void PickColor(Func<string, string> transform)
    {
        _colorFlyout.Hide();
        ReplaceSelection(transform);
    }

    /// <summary>メニューを開くとき、選んでいる文字を控え、押せる項目と、色の見本を合わせる</summary>
    /// <param name="sender">メニュー</param>
    /// <param name="e">イベントの情報</param>
    private void OnBodyMenuOpening(object? sender, object e)
    {
        _selectionStart = BodyBox.SelectionStart;
        _selectionLength = BodyBox.SelectionLength;

        foreach (var item in _selectionItems)
        {
            item.IsEnabled = _selectionLength > 0;
        }

    }

    /// <summary>メニューを開いたときに選んでいた文字を、変換した文字に置き換える</summary>
    /// <param name="transform">選んでいた文字から、置き換える文字を作る処理</param>
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
    /// <remarks>文字を選んでいるときは、その文字がリンク名の初期値になる。</remarks>
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
    /// <returns>入力した内容。キャンセルなら null</returns>
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

    /// <summary>ファイル・フォルダーを落としたら、カーソルの位置に、リンクを入れる</summary>
    /// <param name="sender">本文欄</param>
    /// <param name="e">ドロップの情報</param>
    /// <remarks>リンク名は、ファイル・フォルダーの名前。複数を落としたときは、1 つずつ改行して並べる。</remarks>
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

    /// <summary><c>#RRGGBB</c> の文字から、色にする</summary>
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
