using System.Text.Json.Serialization;
using MmmSdk.Core.Components.Storage;

namespace MmmTool.WorkItems.Core.Json;

/// <summary>作業リストの JSON 読み書き用のシリアライザ設定</summary>
/// <remarks>発行時のトリミングでも動くよう、JSON の読み書きはソース生成で行う。設定は SDK の <see cref="ReadableJsonOptions"/> で揃える。</remarks>
[JsonSerializable(typeof(WorkItemFile))]
[JsonSerializable(typeof(WorkRecordFile))]
[JsonSerializable(typeof(WorkItemChangeFile))]
[JsonSerializable(typeof(WorkProgressFile))]
internal sealed partial class WorkItemJsonContext : JsonSerializerContext
{
    /// <summary>手で読み書きしやすい形の設定</summary>
    public static WorkItemJsonContext Readable { get; } = new(ReadableJsonOptions.Create());
}
