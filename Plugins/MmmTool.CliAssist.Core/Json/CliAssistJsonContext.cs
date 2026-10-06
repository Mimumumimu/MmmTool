using System.Text.Json.Serialization;
using MmmSdk.Core.Components.Storage;

namespace MmmTool.CliAssist.Core.Json;

/// <summary>CLI補助の JSON 読み書き用のシリアライザ設定</summary>
/// <remarks>発行時のトリミングでも動くよう、JSON の読み書きはソース生成で行う。Context は機能ごとに分け、設定は SDK の <see cref="ReadableJsonOptions"/> で揃える。</remarks>
[JsonSerializable(typeof(CliCommandSet))]
[JsonSerializable(typeof(CliSettings))]
internal sealed partial class CliAssistJsonContext : JsonSerializerContext
{
    /// <summary>手で読み書きしやすい形の設定</summary>
    public static CliAssistJsonContext Readable { get; } = new(ReadableJsonOptions.Create());
}
