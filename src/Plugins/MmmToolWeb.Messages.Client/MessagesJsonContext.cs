using System.Text.Json;
using System.Text.Json.Serialization;
using MmmToolWeb.Features.Client;

namespace MmmToolWeb.Messages.Client;

/// <summary>
/// メッセージの API の JSON を読み書きする、ソース生成の設定。サーバーとブラウザが同じものを使う。
/// </summary>
/// <remarks>
/// ブラウザの発行では、使っていない型が削られるので、型を書いて調べる方式 (リフレクション)に頼らない。
/// </remarks>
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(MessageFormResponse))]
[JsonSerializable(typeof(MessageSendRequestDto))]
[JsonSerializable(typeof(ErrorResponse))]
public sealed partial class MessagesJsonContext : JsonSerializerContext;
