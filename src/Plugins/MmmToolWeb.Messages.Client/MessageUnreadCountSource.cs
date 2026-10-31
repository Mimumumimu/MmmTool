using MmmToolWeb.Features.Client;

namespace MmmToolWeb.Messages.Client;

/// <summary>
/// メッセージの、読んでいない数を、サーバーに聞く。ホーム画面のタイルの数に使う。
/// </summary>
/// <param name="client">サーバーのメッセージの API。</param>
public sealed class MessageUnreadCountSource(MessagesClient client) : IUnreadCountSource
{
    /// <inheritdoc />
    public string Kind => MessageStore.MessageKind;

    /// <inheritdoc />
    public async Task<int?> GetCountAsync()
    {
        var result = await client.LoadUnreadCountAsync();
        return result.Succeeded ? result.Value!.Count : null;
    }
}
