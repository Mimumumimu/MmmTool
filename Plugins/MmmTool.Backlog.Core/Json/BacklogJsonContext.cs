using System.Text.Json;
using System.Text.Json.Serialization;

namespace MmmTool.Backlog.Core.Json;

/// <summary>Backlog の API の応答を読むためのシリアライザ設定</summary>
/// <remarks>
/// 応答の形は Backlog が決めているので、保存ファイル用の設定 (SDK の ReadableJsonOptions)ではなく、
/// Web の既定 (camelCase・大文字小文字を区別しない)で読む。発行時のトリミングでも動くよう、ソース生成で行う。
/// </remarks>
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(List<BacklogSharedFile>))]
internal sealed partial class BacklogJsonContext : JsonSerializerContext;
