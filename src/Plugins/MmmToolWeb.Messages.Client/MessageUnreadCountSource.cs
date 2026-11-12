using MmmToolWeb.Features.Client;

namespace MmmToolWeb.Messages.Client;

/// <summary>メッセージの読んでいない数をサーバーに聞く</summary>
/// <param name="client">サーバーのメッセージの API</param>
/// <remarks>ホーム画面のタイルの数に使う</remarks>
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
