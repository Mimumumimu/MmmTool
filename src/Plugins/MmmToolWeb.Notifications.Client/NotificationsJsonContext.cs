using System.Text.Json;
using System.Text.Json.Serialization;
using MmmToolWeb.Features.Client;

namespace MmmToolWeb.Notifications.Client;

/// <summary>通知の API の JSON を読み書きするソース生成の設定</summary>
/// <remarks>
/// サーバーとブラウザが同じものを使う。
/// ブラウザの発行では使っていない型が削られるので、型を書いて調べる方式 (リフレクション)に頼らない。
/// </remarks>
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(PushSettingsResponse))]
[JsonSerializable(typeof(PushRegisterRequest))]
[JsonSerializable(typeof(PushRegisterResponse))]
[JsonSerializable(typeof(PushLookupRequest))]
[JsonSerializable(typeof(PushLookupResponse))]
[JsonSerializable(typeof(PushRemoveRequest))]
[JsonSerializable(typeof(ReceivedListResponse))]
[JsonSerializable(typeof(DestinationResponse))]
[JsonSerializable(typeof(NotificationTargetSettingDto))]
[JsonSerializable(typeof(NotificationChannelRequest))]
[JsonSerializable(typeof(NotificationFeatureDto[]))]
[JsonSerializable(typeof(NotificationFeatureRequest))]
[JsonSerializable(typeof(ErrorResponse))]
public sealed partial class NotificationsJsonContext : JsonSerializerContext;
