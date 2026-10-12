using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MmmSdk.Core.Components.Hosting;
using MmmSdk.WinUI.Components.Windowing;
using MmmSdk.WinUI.Utilities;
using Windows.Graphics;

namespace MmmTool.WorkItems.Report;

/// <summary>作業報告のウィンドウ</summary>
/// <remarks>
/// 親ウィンドウの上に、モーダル (閉じるまで親を操作できない)で出す。大きさは変えられる (最大化・最小化はできない)。
/// タイトル帯をドラッグして移動できる。開くたびに作り直す (閉じたウィンドウは再表示できないため)。
/// </remarks>
public sealed partial class WorkReportWindow : Window
{
    /// <summary>最初の幅 (DIP)</summary>
    private const double InitialWidth = 720;

    /// <summary>最初の高さ (DIP)</summary>
    private const double InitialHeight = 620;

    /// <summary>最小の幅 (DIP)</summary>
    private const double MinimumWidth = 560;

    /// <summary>最小の高さ (DIP)</summary>
    private const double MinimumHeight = 360;

    /// <summary>閉じたら完了する</summary>
    private readonly TaskCompletionSource _closed = new();

    /// <summary>擬似モーダルの処理</summary>
    private readonly PseudoModal _modal;

    /// <summary>ウィンドウの ViewModel</summary>
    public WorkReportViewModel ViewModel { get; }

    /// <summary>ウィンドウを作る</summary>
    /// <param name="viewModel">ウィンドウの ViewModel</param>
    /// <param name="environment">アプリの名前と、データ・アイコンの置き場所</param>
    public WorkReportWindow(WorkReportViewModel viewModel, AppEnvironment environment)
    {
        ViewModel = viewModel;
        InitializeComponent();

        const string dateFormat = "{year.full}/{month.integer(2)}/{day.integer(2)}";
        ReportDatePicker.DateFormat = dateFormat;
        FromDatePicker.DateFormat = dateFormat;

        this.UseCustomTitleBar(TitleBarArea, environment.IconPath);
        _modal = new PseudoModal(this);
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        Closed += (_, _) =>
        {
            ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _closed.TrySetResult();
        };
    }

    /// <summary>親ウィンドウの上にモーダルで表示し、閉じるまで待つ</summary>
    /// <param name="owner">親ウィンドウ</param>
    /// <returns>閉じるまでの待機を表すタスク</returns>
    public async Task ShowModalAsync(Window owner)
    {
        // 空のプレビューが一瞬見えないよう、作ってから出す
        await ViewModel.InitializeAsync();
        ReportDatePicker.Date = ToOffset(ViewModel.ReportDate);
        FromDatePicker.Date = ToOffset(ViewModel.FromDate);

        _modal.SetOwner(owner);
        var scale = _modal.OwnerScale;
        this.UseFixedPresenter(isDialog: true, isResizable: true, new SizeInt32((int)(MinimumWidth * scale), (int)(MinimumHeight * scale)));
        this.ResizeClientDip(InitialWidth, InitialHeight, scale);
        _modal.CenterOnOwner();

        _modal.Show();
        await _closed.Task;
    }

    /// <summary>報告日が選ばれたら、ViewModel に反映する</summary>
    /// <param name="sender">日付の選択欄</param>
    /// <param name="args">日付の変更の情報</param>
    private void OnReportDateChanged(CalendarDatePicker sender, CalendarDatePickerDateChangedEventArgs args)
    {
        if (args.NewDate is { } date)
        {
            ViewModel.ReportDate = DateOnly.FromDateTime(date.Date);
        }
        else
        {
            // 日付がクリアされたときは、今の報告日に戻す (報告日は、いつも決まっている)
            sender.Date = ToOffset(ViewModel.ReportDate);
        }
    }

    /// <summary>実績の開始日が選ばれたら、ViewModel に反映する</summary>
    /// <param name="sender">日付の選択欄</param>
    /// <param name="args">日付の変更の情報</param>
    private void OnFromDateChanged(CalendarDatePicker sender, CalendarDatePickerDateChangedEventArgs args)
    {
        if (args.NewDate is { } date)
        {
            ViewModel.FromDate = DateOnly.FromDateTime(date.Date);
        }
        else
        {
            // 日付がクリアされたときは、今の実績の開始日に戻す (実績の開始日は、いつも決まっている)
            sender.Date = ToOffset(ViewModel.FromDate);
        }
    }

    /// <summary>ViewModel の日付が変わったら、日付の選択欄を合わせる (付け替え・前日への修正)</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">変更されたプロパティの情報</param>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(WorkReportViewModel.ReportDate) when ReportDatePicker.Date?.Date != ViewModel.ReportDate.ToDateTime(TimeOnly.MinValue):
                ReportDatePicker.Date = ToOffset(ViewModel.ReportDate);
                break;
            case nameof(WorkReportViewModel.FromDate) when FromDatePicker.Date?.Date != ViewModel.FromDate.ToDateTime(TimeOnly.MinValue):
                FromDatePicker.Date = ToOffset(ViewModel.FromDate);
                break;
        }
    }

    /// <summary>日付を、日付の選択欄の値にする</summary>
    /// <param name="date">日付</param>
    /// <returns>その日の 0 時 (この PC のタイムゾーン)</returns>
    private static DateTimeOffset ToOffset(DateOnly date) => new(date.ToDateTime(TimeOnly.MinValue));

    /// <summary>「閉じる」が押されたら、ウィンドウを閉じる</summary>
    /// <param name="sender">ボタン</param>
    /// <param name="e">イベントの情報</param>
    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
}
