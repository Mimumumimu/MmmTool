namespace MmmTool.Core.Reminders.Json;

/// <summary>Reminders.json の中身</summary>
/// <remarks>リンクの JSON と同じく <c>{ "items": [...] }</c> の形にする。</remarks>
internal sealed class ReminderFile
{
    /// <summary>リマインダー本体の一覧</summary>
    public List<Reminder>? Items { get; set; } = [];
}
