using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MmmTool.NotificationChannels.Settings;

/// <summary>設定ページの「通知先」に並べる通知先の選択</summary>
/// <param name="options">
/// 選べる送信先
/// 先頭は「指定しない」
/// </param>
/// <param name="changed">
/// 選択が変わったときの処理
/// 保存する
/// </param>
/// <remarks>人ごとに 1 つの共通の設定</remarks>
public sealed partial class NotificationTargetItem(IReadOnlyList<NotificationChannelOption> options, Func<NotificationTargetItem, Task> changed) : ObservableObject
{
    /// <summary>画面に出す通知先の名前</summary>
    public string Name => "通知先";

    /// <summary>選べる送信先</summary>
    public ObservableCollection<NotificationChannelOption> Options { get; } = [.. options];

    /// <summary>選んでいる送信先</summary>
    /// <remarks>読み込み中は保存しない</remarks>
    [ObservableProperty]
    public partial NotificationChannelOption? Selected { get; set; }

    /// <summary>読み込みで選択を決めている最中か</summary>
    /// <remarks>true の間は選択の変わりを保存しない</remarks>
    public bool IsLoading { get; set; }

    /// <summary>選択が変わったとき、読み込み中でなければ保存する</summary>
    /// <param name="value">新しい選択</param>
    partial void OnSelectedChanged(NotificationChannelOption? value)
    {
        if (!IsLoading && value is not null)
        {
            _ = changed(this);
        }
    }
}
