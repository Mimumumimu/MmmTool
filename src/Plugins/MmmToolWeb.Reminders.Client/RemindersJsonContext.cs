using System.Text.Json;
using System.Text.Json.Serialization;
using MmmToolWeb.Features.Client;

namespace MmmToolWeb.Reminders.Client;

/// <summary>
/// リマインダーの API の JSON を読み書きする、ソース生成の設定。サーバーとブラウザが同じものを使う。
/// </summary>
/// <remarks>
/// ブラウザの発行では、使っていない型が削られるので、型を書いて調べる方式 (リフレクション)に頼らない。
/// </remarks>
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(ReminderTodayResponse))]
[JsonSerializable(typeof(ReminderListResponse))]
[JsonSerializable(typeof(ReminderDetailDto))]
[JsonSerializable(typeof(ReminderSaveRequest))]
[JsonSerializable(typeof(ReminderSaveResult))]
[JsonSerializable(typeof(ReminderDeviceRequest))]
[JsonSerializable(typeof(ReminderDeviceResponse))]
[JsonSerializable(typeof(ReminderDeletedRequest))]
[JsonSerializable(typeof(ReminderOptionsResponse))]
[JsonSerializable(typeof(ErrorResponse))]
public sealed partial class RemindersJsonContext : JsonSerializerContext;
