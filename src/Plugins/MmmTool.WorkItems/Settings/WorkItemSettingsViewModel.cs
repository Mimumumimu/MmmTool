using CommunityToolkit.Mvvm.ComponentModel;
using MmmSdk.Core.Components.Storage;
using MmmSdk.Core.Utilities;
using MmmSdk.WinUI.Components.Errors;
using MmmTool.WorkItems.Core;

namespace MmmTool.WorkItems.Settings;

/// <summary>作業管理の設定 (設定ページの部品)の ViewModel</summary>
public sealed partial class WorkItemSettingsViewModel : ObservableObject
{
    /// <summary>起動時の表示の設定</summary>
    private readonly WorkItemDisplaySettings _displaySettings;

    /// <summary>保存済みの値を入れている最中か</summary>
    /// <remarks>読み込んだだけの値で設定ファイルを書き換えないよう、true の間は、値の変更で保存しない。</remarks>
    private bool _isInitializing;

    /// <summary>画面に出すエラー (保存に失敗したとき)</summary>
    public ErrorState Error { get; } = new();

    /// <summary>設定を変更できるか (設定ファイルを読めなかったときは、上書きして消さないよう、変更させない)</summary>
    public bool IsEditable { get; }

    /// <summary>作業管理の「過去の完了」の、起動時の表示の選択肢の番号 (<see cref="CompletedDisplay"/> の値と同じ)</summary>
    /// <remarks>変わったら即保存する。</remarks>
    [ObservableProperty]
    public partial int PastCompletedIndex { get; set; }

    /// <summary>スケジュールの「完了」の、起動時の表示の選択肢の番号 (<see cref="CompletedDisplay"/> の値と同じ)</summary>
    /// <remarks>変わったら即保存する。</remarks>
    [ObservableProperty]
    public partial int ScheduleCompletedIndex { get; set; }

    /// <summary>保存済みの値を読み込んで表示する</summary>
    /// <param name="displaySettings">起動時の表示の設定</param>
    public WorkItemSettingsViewModel(WorkItemDisplaySettings displaySettings)
    {
        _displaySettings = displaySettings;
        _isInitializing = true;
        PastCompletedIndex = (int)displaySettings.PastCompleted;
        ScheduleCompletedIndex = (int)displaySettings.ScheduleCompleted;
        _isInitializing = false;
        IsEditable = !displaySettings.IsReadOnly;
    }

    /// <summary>「過去の完了」の選択が変わったら保存する</summary>
    /// <param name="value">選択肢の番号</param>
    partial void OnPastCompletedIndexChanged(int value)
    {
        if (!_isInitializing && Enum.IsDefined((CompletedDisplay)value))
        {
            SaveAsync(() => _displaySettings.SetPastCompletedAsync((CompletedDisplay)value)).Forget();
        }
    }

    /// <summary>「完了」の選択が変わったら保存する</summary>
    /// <param name="value">選択肢の番号</param>
    partial void OnScheduleCompletedIndexChanged(int value)
    {
        if (!_isInitializing && Enum.IsDefined((CompletedDisplay)value))
        {
            SaveAsync(() => _displaySettings.SetScheduleCompletedAsync((CompletedDisplay)value)).Forget();
        }
    }

    /// <summary>設定を保存する</summary>
    /// <param name="save">保存する処理</param>
    /// <returns>保存の完了を表すタスク</returns>
    /// <remarks>
    /// 保存できない状態 (設定ファイルを読めなかった)では、選択欄を無効にしてあるので、ここへは来ない。来たら、保存されなかったことを知らせる。
    /// 保存の失敗 (ロック・権限など)は、画面に出す。
    /// </remarks>
    private async Task SaveAsync(Func<Task<bool>> save)
    {
        try
        {
            if (await save())
            {
                Error.Clear();
            }
            else
            {
                Error.Show("設定を読み込めなかったため、変更を保存できませんでした。");
            }
        }
        catch (DataFileException ex)
        {
            Error.Show(ex.Message);
        }
    }
}
