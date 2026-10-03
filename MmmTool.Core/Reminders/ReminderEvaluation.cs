using MmmSdk.Core.Components.Notifications;

namespace MmmTool.Core.Reminders;

/// <summary>判定の結果（今の分に通知する項目）</summary>
/// <param name="Items">通知する項目。無ければ空</param>
/// <param name="IncludesSnooze">スヌーズ分を含むか（含むなら、次のスヌーズの間隔をこの分から数え直す）</param>
public sealed record ReminderEvaluation(IReadOnlyList<NotificationItem> Items, bool IncludesSnooze);
