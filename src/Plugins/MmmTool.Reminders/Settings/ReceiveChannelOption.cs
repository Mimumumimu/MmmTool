using MmmTool.NotificationChannels.Core;

namespace MmmTool.Reminders.Settings;

/// <summary>
/// 受け取り先に選べる送信先 1 件。指定しない選択も 1 件として持つ。
/// </summary>
/// <param name="ChannelId">送信先の番号。指定しないときは 0</param>
/// <param name="Text">画面に出す文字</param>
/// <remarks>送る先の値は持たない。秘密のため。</remarks>
public sealed record ReceiveChannelOption(int ChannelId, string Text)
{
    /// <summary>指定しない選択</summary>
    public static ReceiveChannelOption None { get; } = new(0, "指定しない");

    /// <summary>送信先から、選択肢を作る</summary>
    /// <param name="channel">送信先</param>
    /// <returns>「登録名 (種類)」の形の選択肢</returns>
    public static ReceiveChannelOption From(NotificationChannel channel)
    {
        ArgumentNullException.ThrowIfNull(channel);

        return new ReceiveChannelOption(channel.Id, $"{channel.Name} ({NotificationChannelKinds.ToText(channel.Kind)})");
    }
}
