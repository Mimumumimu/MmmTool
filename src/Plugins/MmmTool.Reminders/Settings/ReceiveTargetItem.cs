using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MmmTool.Reminders.Settings;

/// <summary>
/// 設定ページの「受け取り先」に並べる、通知の受け取り先の選択。人ごとに 1 つの共通の設定。
/// </summary>
/// <param name="options">選べる送信先。先頭は「指定しない」</param>
/// <param name="changed">選択が変わったときの処理。保存する</param>
public sealed partial class ReceiveTargetItem(IReadOnlyList<ReceiveChannelOption> options, Func<ReceiveTargetItem, Task> changed) : ObservableObject
{
    /// <summary>画面に出す、受け取り先の名前</summary>
    public string Name => "通知の受け取り先";

    /// <summary>選べる送信先</summary>
    public ObservableCollection<ReceiveChannelOption> Options { get; } = [.. options];

    /// <summary>選んでいる送信先。読み込み中は、保存しない</summary>
    [ObservableProperty]
    public partial ReceiveChannelOption? Selected { get; set; }

    /// <summary>読み込みで選択を決めている最中か。true の間は、選択の変わりを保存しない</summary>
    public bool IsLoading { get; set; }

    /// <summary>選択が変わったとき、読み込み中でなければ保存する</summary>
    /// <param name="value">新しい選択</param>
    partial void OnSelectedChanged(ReceiveChannelOption? value)
    {
        if (!IsLoading && value is not null)
        {
            _ = changed(this);
        }
    }
}
