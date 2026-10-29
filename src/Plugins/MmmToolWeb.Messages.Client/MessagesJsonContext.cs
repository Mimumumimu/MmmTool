using System.Text.Json;
using System.Text.Json.Serialization;
using MmmToolWeb.Features.Client;

namespace MmmToolWeb.Messages.Client;

/// <summary>
/// メッセージの API の JSON を読み書きする、ソース生成の設定。サーバーとブラウザが同じものを使う。
/// </summary>
/// <remarks>
/// ブラウザの発行では、使っていない型が削られるので、型を書いて調べる方式 (リフレクション)に頼らない。
/// 端末の控えに残す JSON も、この設定で読み書きする。
/// </remarks>
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(MessageSyncDto))]
[JsonSerializable(typeof(MessagePageDto))]
[JsonSerializable(typeof(MessageSendDto))]
[JsonSerializable(typeof(MessageSentDto))]
[JsonSerializable(typeof(PersonDto[]))]
[JsonSerializable(typeof(DirectDto))]
[JsonSerializable(typeof(MessageFormDto))]
[JsonSerializable(typeof(MessageCacheState))]
[JsonSerializable(typeof(ConversationCacheState))]
[JsonSerializable(typeof(ErrorResponse))]
public sealed partial class MessagesJsonContext : JsonSerializerContext;
