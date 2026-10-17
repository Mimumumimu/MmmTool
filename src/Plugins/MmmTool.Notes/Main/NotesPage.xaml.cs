using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MmmSdk.Core.Components.Hosting;
using MmmSdk.Core.Utilities;

namespace MmmTool.Notes.Main;

/// <summary>ノートのページ (サイドバー)。その日に有効なノートを、カードに並べた 1 枚の画面で読む</summary>
public sealed partial class NotesPage : Page
{
    /// <summary>ページが載っている画面 (見える・見えないの変化を受けるため。載っていない間は null)</summary>
    private XamlRoot? _root;

    /// <summary>読み直している (見えている)状態か</summary>
    private bool _active;

    /// <summary>ページの ViewModel</summary>
    public NotesViewModel ViewModel { get; }

    /// <summary>ページを作る</summary>
    /// <param name="viewModel">ページの ViewModel</param>
    /// <param name="environment">アプリの名前と、データ・アイコンの置き場所</param>
    public NotesPage(NotesViewModel viewModel, AppEnvironment environment)
    {
        ViewModel = viewModel;
        InitializeComponent();

        DatePicker.Language = "ja-JP";
        DatePicker.DateFormat = "{year.full}/{month.integer(2)}/{day.integer(2)} ({dayofweek.abbreviated})";
        DatePicker.Date = ToOffset(ViewModel.Day);

        CardsView.WebView2Directory = environment.WebView2Directory;
        ViewModel.Surface = CardsView;
        CardsView.ActionRequested += (_, e) => ViewModel.HandleActionAsync(e.Action, e.NoteId).Forget();
        CardsView.LinkRequested += (_, uri) => ViewModel.OpenLinkAsync(uri).Forget();

        ViewModel.PropertyChanged += (_, e) =>
        {
            // 「前の日」「今日」などで日付が変わったら、日付の欄も合わせる
            if (e.PropertyName == nameof(NotesViewModel.Day) && DatePicker.Date?.Date != ViewModel.Day.ToDateTime(TimeOnly.MinValue))
            {
                DatePicker.Date = ToOffset(ViewModel.Day);
            }
        };

        // 見えている間だけ、ほかの人の更新を読み直す (ほかのページを見ている間と、ウィンドウをトレイへ隠している間は、DB に問い合わせない)
        Loaded += (_, _) =>
        {
            ViewModel.IsDark = ActualTheme == ElementTheme.Dark;
            _root = XamlRoot;
            _root.Changed += OnXamlRootChanged;
            UpdateActive();
        };
        Unloaded += (_, _) =>
        {
            if (_root is not null)
            {
                _root.Changed -= OnXamlRootChanged;
                _root = null;
            }
            SetActive(false);
        };
        ActualThemeChanged += (_, _) =>
        {
            ViewModel.IsDark = ActualTheme == ElementTheme.Dark;
            ViewModel.ThemeChangedAsync().Forget();
        };
    }

    /// <summary>ページが載っているウィンドウの表示の状態が変わったら、読み直しを始める・止める</summary>
    /// <param name="sender">ウィンドウの内容の画面 (<c>XamlRoot</c>)</param>
    /// <param name="args">変更の情報</param>
    /// <remarks>ウィンドウは、× でトレイへ隠す (ページの <c>Unloaded</c> は来ない)ので、ここで見える・見えないを知る。大きさの変更などでも来るが、状態が変わったときだけ動く。</remarks>
    private void OnXamlRootChanged(XamlRoot sender, XamlRootChangedEventArgs args) => UpdateActive();

    /// <summary>ページが、見えている (読み直す)状態かを、今の画面から決める</summary>
    private void UpdateActive() => SetActive(_root?.IsHostVisible == true);

    /// <summary>読み直しを始める・止める (状態が変わったときだけ)</summary>
    /// <param name="active">見えているか</param>
    private void SetActive(bool active)
    {
        if (active == _active)
        {
            return;
        }

        _active = active;
        if (active)
        {
            ViewModel.ResumeAsync().Forget();
        }
        else
        {
            ViewModel.Suspend();
        }
    }

    /// <summary>日付の欄が変わったら、表示する日を変える</summary>
    /// <param name="sender">日付の欄</param>
    /// <param name="args">変更の情報</param>
    private void OnDateChanged(CalendarDatePicker sender, CalendarDatePickerDateChangedEventArgs args)
    {
        if (args.NewDate is { } date)
        {
            ViewModel.Day = DateOnly.FromDateTime(date.Date);
        }
        else
        {
            // 日付がクリアされたときは、今の日に戻す (表示する日は、いつも決まっている)
            sender.Date = ToOffset(ViewModel.Day);
        }
    }

    /// <summary>日付を、日付の欄に渡す形にする</summary>
    /// <param name="date">日付</param>
    /// <returns>その日の 0 時 (この PC の時差)</returns>
    private static DateTimeOffset ToOffset(DateOnly date) => new(date.ToDateTime(TimeOnly.MinValue));
}
