using MmmSdk.Core.Components.Settings;

namespace MmmTool.WorkItems.Core;

/// <summary>
/// 作業管理とスケジュールの、起動時の表示の設定。汎用設定ストアに、この PC の設定として保存・取得する。
/// </summary>
/// <param name="settings">汎用設定ストア</param>
/// <remarks>
/// 画面で選んだ表示は保存しない。ここで持つのは、画面を作るときの初期値だけ。
/// 表示の好みは PC ごとのものなので、DB モードでも DB には置かない。
/// </remarks>
public sealed class WorkItemDisplaySettings(ISettingsStore settings)
{
    /// <summary>作業管理の「過去の完了」の、起動時の表示の設定キー</summary>
    /// <remarks><see cref="CompletedDisplay"/> の値の整数。無ければ、<see cref="DefaultPastCompleted"/>。</remarks>
    private const string PastCompletedKey = "WorkItems.InitialPastCompleted";

    /// <summary>スケジュールの「完了」の、起動時の表示の設定キー</summary>
    /// <remarks><see cref="CompletedDisplay"/> の値の整数。無ければ、<see cref="DefaultScheduleCompleted"/>。</remarks>
    private const string ScheduleCompletedKey = "WorkItems.InitialScheduleCompleted";

    /// <summary>作業管理の「過去の完了」の、起動時の表示の既定</summary>
    public const CompletedDisplay DefaultPastCompleted = CompletedDisplay.GroupsOnly;

    /// <summary>スケジュールの「完了」の、起動時の表示の既定</summary>
    public const CompletedDisplay DefaultScheduleCompleted = CompletedDisplay.Show;

    /// <summary>作業管理の「過去の完了」の、起動時の表示 (保存値が無い・選択肢に無いときは既定)</summary>
    public CompletedDisplay PastCompleted => Get(PastCompletedKey, DefaultPastCompleted);

    /// <summary>スケジュールの「完了」の、起動時の表示 (保存値が無い・選択肢に無いときは既定)</summary>
    public CompletedDisplay ScheduleCompleted => Get(ScheduleCompletedKey, DefaultScheduleCompleted);

    /// <summary>設定を保存できない状態か (設定ファイルを読めなかったため、元のファイルを上書きしないよう保存を止めている)</summary>
    public bool IsReadOnly => settings.IsReadOnly;

    /// <summary>作業管理の「過去の完了」の、起動時の表示を保存する</summary>
    /// <param name="value">保存する表示</param>
    /// <returns>保存したら true。設定を保存できない状態 (<see cref="IsReadOnly"/>)で保存しなかったら false</returns>
    public Task<bool> SetPastCompletedAsync(CompletedDisplay value) => settings.SetAsync(PastCompletedKey, (int)value);

    /// <summary>スケジュールの「完了」の、起動時の表示を保存する</summary>
    /// <param name="value">保存する表示</param>
    /// <returns>保存したら true。設定を保存できない状態 (<see cref="IsReadOnly"/>)で保存しなかったら false</returns>
    public Task<bool> SetScheduleCompletedAsync(CompletedDisplay value) => settings.SetAsync(ScheduleCompletedKey, (int)value);

    /// <summary>保存された表示を読む</summary>
    /// <param name="key">設定のキー</param>
    /// <param name="defaultValue">保存値が無い・選択肢に無いときの表示</param>
    /// <returns>保存された表示。無い・選択肢に無いときは既定</returns>
    private CompletedDisplay Get(string key, CompletedDisplay defaultValue)
    {
        var saved = (CompletedDisplay)settings.Get(key, (int)defaultValue);
        return Enum.IsDefined(saved) ? saved : defaultValue;
    }
}
