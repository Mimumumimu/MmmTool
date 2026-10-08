using CommunityToolkit.Mvvm.ComponentModel;
using MmmTool.Reminders.Core;

namespace MmmTool.Reminders.Input;

/// <summary>
/// 入力画面で選ぶ、リマインダーの送信先 (チェックのリストの 1 行)。
/// </summary>
/// <param name="id">送信先の番号 (<c>NotificationChannel.Id</c>)</param>
/// <param name="kind">送信先の区分。分からなければ null</param>
/// <param name="name">送信先の登録名</param>
/// <param name="isChecked">最初に選んでいるか</param>
public sealed partial class ReminderChannelOption(int id, NotificationChannelKind? kind, string name, bool isChecked) : ObservableObject
{
    /// <summary>送信先の番号</summary>
    public int Id { get; } = id;

    /// <summary>画面に出す名前 (種類と登録名。例: ntfy「リマインダー」)</summary>
    public string Text { get; } = kind is { } value ? $"{NotificationChannelKinds.ToText(value)}「{name}」" : $"「{name}」";

    /// <summary>選んでいるか</summary>
    [ObservableProperty]
    public partial bool IsChecked { get; set; } = isChecked;
}
