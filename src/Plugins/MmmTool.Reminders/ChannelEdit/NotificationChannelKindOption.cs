using MmmTool.Reminders.Core;

namespace MmmTool.Reminders.ChannelEdit;

/// <summary>
/// 登録画面で選ぶ、送信先の種類 (選択肢)。
/// </summary>
/// <param name="Kind">区分</param>
/// <param name="Name">画面に出す名前</param>
public sealed record NotificationChannelKindOption(NotificationChannelKind Kind, string Name);
