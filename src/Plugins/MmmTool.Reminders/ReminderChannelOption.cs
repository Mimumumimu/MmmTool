using CommunityToolkit.Mvvm.ComponentModel;
using MmmTool.NotificationChannels.Core;

namespace MmmTool.Reminders;

/// <summary>リマインダーの画面で選ぶ送信先のチェックのリストの 1 行</summary>
/// <param name="id">送信先の番号 (<c>NotificationChannel.Id</c>)</param>
/// <param name="kind">
/// 送信先の区分
/// 分からなければ null
/// </param>
/// <param name="name">送信先の登録名</param>
/// <param name="isChecked">最初に選んでいるか</param>
/// <remarks>入力画面とまとめて追加の画面が共有する</remarks>
public sealed partial class ReminderChannelOption(int id, NotificationChannelKind? kind, string name, bool isChecked) : ObservableObject
{
    /// <summary>送信先の番号</summary>
    public int Id { get; } = id;

    /// <summary>画面に出す名前で、登録名と種類</summary>
    /// <remarks>例は リマインダー</remarks>
    public string Text { get; } = kind is { } value ? $"{name} ({NotificationChannelKinds.ToText(value)})" : name;

    /// <summary>選んでいるか</summary>
    [ObservableProperty]
    public partial bool IsChecked { get; set; } = isChecked;
}
