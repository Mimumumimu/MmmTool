using MmmTool.Reminders.Core;

namespace MmmTool.Reminders.ChannelList;

/// <summary>送信先の一覧の 1 行</summary>
/// <param name="source">元の送信先</param>
/// <remarks>送る先の値 (<see cref="NotificationChannel.Value"/>)は秘密なので、行には持たない。一覧は変更があるたびに作り直すので、値は変わらない。</remarks>
public sealed class NotificationChannelItem(NotificationChannel source)
{
    /// <summary>元の送信先</summary>
    public NotificationChannel Source { get; } = source;

    /// <summary>種類の表示 (「ntfy」「Discord」)</summary>
    public string KindText { get; } = NotificationChannelKinds.ToText(source.Kind);

    /// <summary>登録名</summary>
    public string Name => Source.Name;
}
