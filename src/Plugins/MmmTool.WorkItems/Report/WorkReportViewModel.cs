using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MmmSdk.Core.Components.Settings;
using MmmSdk.Core.Components.Storage;
using MmmSdk.Core.Utilities;
using MmmSdk.WinUI.Components.Clipboards;
using MmmSdk.WinUI.Components.Errors;
using MmmTool.WorkItems.Core;

namespace MmmTool.WorkItems.Report;

/// <summary>作業報告の画面</summary>
/// <param name="service">作業管理の読み書き</param>
/// <param name="settings">この PC の設定の保存先 (「実績に時間を出す」「進捗度を出す」の選択を覚える)</param>
/// <param name="clipboard">クリップボード</param>
/// <remarks>
/// 実績の開始日と報告日を決めて、実績と予定のテキストを作り、クリップボードへコピーする。
/// 報告日の既定は作業管理の入力日。
/// 実績の開始日の既定は報告日より前で、担当が自分の作業に工数が入った日か、作業を完了にした日の、直近の日で、無ければ前日。
/// 報告日を変えたとき、実績の開始日は常に、新しい報告日の既定に付け替える。
/// これは連動で、手で変えた実績の開始日も、報告日を変えると戻る。
/// 実績の開始日だけを変えたときはそのまま残す。
/// 報告日以降にならないよう、前日までに直す。
/// 保存先から読むのは実績の開始日・報告日が変わったときだけ。
/// 「実績に時間を出す」「進捗度を出す」「保留を表示」が変わったときは読んだ内容から作り直す。
/// 「保留を表示」は設定に覚えず、開くたびにオンから始める。
/// 内容の規則は <see cref="WorkReportBuilder"/>。
/// 画面の表示の設定 (隠した行・表示する案件)には従わない。
/// </remarks>
public sealed partial class WorkReportViewModel(WorkItemService service, ISettingsStore settings, IClipboardService clipboard) : ObservableObject
{
    /// <summary>「実績に時間を出す」の選択を残す設定のキーで、この PC の設定</summary>
    /// <remarks>真偽</remarks>
    public const string ShowHoursKey = WorkReportDayContentSource.ShowHoursKey;

    /// <summary>「進捗度を出す」の選択を残す設定のキーで、この PC の設定</summary>
    /// <remarks>
    /// 真偽
    /// </remarks>
    public const string ShowProgressKey = WorkReportDayContentSource.ShowProgressKey;

    /// <summary>今の読み込みの番号</summary>
    /// <remarks>古い結果を捨てるため</remarks>
    private int _version;

    /// <summary>最初の値を決めている最中か</summary>
    /// <remarks>その間の変更では設定の保存もプレビューの作り直しもしない</remarks>
    private bool _isInitializing;

    /// <summary>実績の開始日をコードで設定している最中か</summary>
    /// <remarks>手で変えたと数えないため</remarks>
    private bool _isSettingFrom;

    /// <summary>最後に読んだ内容</summary>
    private WorkReportData? _data;

    /// <summary>最後に読んだ内容の実績の開始日と報告日</summary>
    /// <remarks>テキストを作るときに使う</remarks>
    private (DateOnly From, DateOnly Report) _dataDates;

    /// <summary>画面に出すエラー</summary>
    public ErrorState Error { get; } = new();

    /// <summary>報告日で、予定の日</summary>
    /// <remarks>実績はこの日より前だけ</remarks>
    [ObservableProperty]
    public partial DateOnly ReportDate { get; set; }

    /// <summary>実績の開始日で、実績を拾い始める日</summary>
    /// <remarks>報告日より前</remarks>
    [ObservableProperty]
    public partial DateOnly FromDate { get; set; }

    /// <summary>実績の各作業に期間内の工数の合計を添えるか</summary>
    [ObservableProperty]
    public partial bool ShowHours { get; set; }

    /// <summary>実績と予定の各作業に進捗度を添えるか</summary>
    [ObservableProperty]
    public partial bool ShowProgress { get; set; }

    /// <summary>保留の区画を出すか</summary>
    /// <remarks>
    /// 除外したいときだけ、そのつど手でオフにする。
    /// 既定はオンで、設定には覚えない。
    /// </remarks>
    [ObservableProperty]
    public partial bool ShowOnHold { get; set; } = true;

    /// <summary>プレビュー</summary>
    [ObservableProperty]
    public partial string PreviewText { get; private set; } = "";

    /// <summary>コピーした直後か</summary>
    /// <remarks>「コピーしました」を出し、内容が変わるまで続ける。</remarks>
    [ObservableProperty]
    public partial bool IsCopied { get; set; }

    /// <summary>コピーできるか</summary>
    /// <remarks>プレビューができている</remarks>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CopyCommand))]
    public partial bool CanCopy { get; private set; }

    /// <summary>最初の値を決めてプレビューを作る</summary>
    /// <param name="reportDate">報告日の初期値 (作業管理で開いている日)</param>
    /// <returns>読み込みの完了を表すタスク</returns>
    public async Task InitializeAsync(DateOnly reportDate)
    {
        _isInitializing = true;
        try
        {
            ShowHours = settings.Get(ShowHoursKey, false);
            ShowProgress = settings.Get(ShowProgressKey, true);
            ReportDate = reportDate;
            try
            {
                SetFrom(await service.GetDefaultReportFromAsync(ReportDate));
            }
            catch (DataFileException ex)
            {
                SetFrom(ReportDate.AddDays(-1));
                Error.Show(ex.Message);
            }
        }
        finally
        {
            _isInitializing = false;
        }
        await ReloadAsync();
    }

    /// <summary>報告日が変わったら、実績の開始日を付け替えて、読み直す</summary>
    /// <param name="value">新しい報告日</param>
    partial void OnReportDateChanged(DateOnly value)
    {
        if (!_isInitializing)
        {
            OnReportDateChangedAsync(value).Forget();
        }
    }

    /// <summary>実績の開始日を新しい報告日の既定に付け替えて読み直す</summary>
    /// <param name="value">新しい報告日</param>
    /// <returns>付け替えと読み直しの完了を表すタスク</returns>
    private async Task OnReportDateChangedAsync(DateOnly value)
    {
        try
        {
            var from = await service.GetDefaultReportFromAsync(value);
            if (value != ReportDate)
            {
                return;
            }
            SetFrom(from);
        }
        catch (DataFileException ex)
        {
            SetFrom(value.AddDays(-1));
            Error.Show(ex.Message);
        }
        if (value == ReportDate)
        {
            await ReloadAsync();
        }
    }

    /// <summary>実績の開始日が変わったら、報告日以降にならないよう直して、読み直す</summary>
    /// <param name="value">新しい実績の開始日</param>
    partial void OnFromDateChanged(DateOnly value)
    {
        if (_isInitializing || _isSettingFrom)
        {
            return;
        }
        if (value >= ReportDate)
        {
            SetFrom(ReportDate.AddDays(-1));
        }
        ReloadAsync().Forget();
    }

    /// <summary>「実績に時間を出す」が変わったら、選択を残して、読んだ内容から作り直す</summary>
    /// <param name="value">新しい値</param>
    partial void OnShowHoursChanged(bool value)
    {
        if (_isInitializing)
        {
            return;
        }
        settings.SetAsync(ShowHoursKey, value).Forget();
        Rebuild();
    }

    /// <summary>「進捗度を出す」が変わったら、選択を残して、読んだ内容から作り直す</summary>
    /// <param name="value">新しい値</param>
    partial void OnShowProgressChanged(bool value)
    {
        if (_isInitializing)
        {
            return;
        }
        settings.SetAsync(ShowProgressKey, value).Forget();
        Rebuild();
    }

    /// <summary>「保留を表示」が変わったら読んだ内容から作り直す</summary>
    /// <param name="value">新しい値</param>
    /// <remarks>設定には残さない</remarks>
    partial void OnShowOnHoldChanged(bool value) => Rebuild();

    /// <summary>実績の開始日をコードで設定する</summary>
    /// <param name="value">設定する日</param>
    /// <remarks>手で変えたことにしない</remarks>
    private void SetFrom(DateOnly value)
    {
        _isSettingFrom = true;
        try
        {
            FromDate = value;
        }
        finally
        {
            _isSettingFrom = false;
        }
    }

    /// <summary>保存先から読み直してプレビューを作り直す</summary>
    /// <returns>読み込みの完了を表すタスク</returns>
    private async Task ReloadAsync()
    {
        var version = ++_version;
        var from = FromDate;
        var report = ReportDate;
        try
        {
            var data = await service.LoadReportAsync(from, report);
            if (version != _version)
            {
                return;
            }
            _data = data;
            _dataDates = (from, report);
            Error.Clear();
            Rebuild();
        }
        catch (DataFileException ex)
        {
            if (version == _version)
            {
                _data = null;
                CanCopy = false;
                Error.Show(ex.Message);
            }
        }
    }

    /// <summary>最後に読んだ内容からプレビューを作り直す</summary>
    /// <remarks>保存先は読まない</remarks>
    private void Rebuild()
    {
        if (_isInitializing || _data is null)
        {
            return;
        }
        IsCopied = false;
        PreviewText = WorkReportBuilder.Build(_data, _dataDates.From, _dataDates.Report, ShowHours, ShowProgress, ShowOnHold);
        CanCopy = true;
    }

    /// <summary>プレビューのテキストをクリップボードにコピーする</summary>
    [RelayCommand(CanExecute = nameof(CanCopy))]
    private void Copy()
    {
        try
        {
            clipboard.SetText(PreviewText);
            IsCopied = true;
            Error.Clear();
        }
        catch (COMException ex)
        {
            Error.Show($"クリップボードに書けませんでした。{ex.Message}");
        }
    }
}
