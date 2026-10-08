using System.Text.Json.Serialization;
using MmmSdk.Core.Components.Storage;

namespace MmmTool.Reminders.Core.Json;

/// <summary>リマインダーの JSON 読み書き用のシリアライザ設定</summary>
/// <remarks>発行時のトリミングでも動くよう、JSON の読み書きはソース生成で行う。Context は機能ごとに分け、設定は SDK の <see cref="ReadableJsonOptions"/> で揃える。</remarks>
[JsonSerializable(typeof(ReminderFile))]
[JsonSerializable(typeof(ReminderStateFile))]
internal sealed partial class ReminderJsonContext : JsonSerializerContext
{
    /// <summary>手で読み書きしやすい形の設定</summary>
    public static ReminderJsonContext Readable { get; } = new(ReadableJsonOptions.Create());
}
