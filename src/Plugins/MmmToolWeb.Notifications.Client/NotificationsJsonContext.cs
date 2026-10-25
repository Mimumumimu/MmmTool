using System.Text.Json;
using System.Text.Json.Serialization;
using MmmToolWeb.Features.Client;

namespace MmmToolWeb.Notifications.Client;

/// <summary>
/// 通知の API の JSON を読み書きする、ソース生成の設定。サーバーとブラウザが同じものを使う。
/// </summary>
/// <remarks>
/// ブラウザの発行では、使っていない型が削られるので、型を書いて調べる方式 (リフレクション)に頼らない。
/// </remarks>
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(PushSettingsResponse))]
[JsonSerializable(typeof(PushRegisterRequest))]
[JsonSerializable(typeof(PushRegisterResponse))]
[JsonSerializable(typeof(PushRemoveRequest))]
[JsonSerializable(typeof(ReceivedListResponse))]
[JsonSerializable(typeof(DestinationResponse))]
[JsonSerializable(typeof(ErrorResponse))]
public sealed partial class NotificationsJsonContext : JsonSerializerContext;
