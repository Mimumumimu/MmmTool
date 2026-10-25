using MmmTool.Messages.Core;
using MmmTool.NotificationChannels.Core;

namespace MmmToolWeb.Messages;

/// <summary>
/// メッセージの通知を押したときに、そのメッセージの会話を開く行き先。
/// </summary>
/// <remarks>
/// 「通知」の機能は、メッセージの名前を持たない。この行き先を登録するのは、メッセージの機能。
/// 開く会話は、そのメッセージの位置まで読み、そのメッセージを強調して出す。
/// </remarks>
/// <param name="repository">会話とメッセージの保存先</param>
public sealed class MessageNotificationDestination(IMessageRepository repository) : INotificationDestination
{
    /// <inheritdoc />
    public string Key => MessageNotificationText.ItemKind;

    /// <inheritdoc />
    public async Task<string?> GetPathAsync(int id, CancellationToken cancellationToken = default)
    {
        var conversationId = await repository.FindConversationOfMessageAsync(id, cancellationToken);
        return conversationId == 0 ? null : $"/messages/{conversationId}?item={Key}:{id}";
    }
}
