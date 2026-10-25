using System.Text.Json;
using System.Text.Json.Serialization;

namespace MmmTool.Messages.Cache;

/// <summary>
/// 端末の控えを読み書きする、ソース生成の設定。
/// </summary>
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(CachedConversationList))]
[JsonSerializable(typeof(CachedMessages))]
internal sealed partial class MessageCacheJsonContext : JsonSerializerContext;
