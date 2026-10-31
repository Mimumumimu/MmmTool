using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MmmTool.CliAssist.FocusTriggers;

/// <summary>編集ダイアログで編集中の、通知する文字列 1 件</summary>
/// <param name="remove">この項目を一覧から取り除く処理</param>
public sealed partial class FocusTriggerEditItem(Action<FocusTriggerEditItem> remove) : ObservableObject
{
    /// <summary>出力に出る文字列</summary>
    [ObservableProperty]
    public partial string Text { get; set; } = string.Empty;

    /// <summary>知らせるときに、ターミナルへフォーカスも移すか</summary>
    [ObservableProperty]
    public partial bool MoveToTerminal { get; set; } = true;

    /// <summary>この項目を一覧から取り除く</summary>
    [RelayCommand]
    private void Remove() => remove(this);
}
