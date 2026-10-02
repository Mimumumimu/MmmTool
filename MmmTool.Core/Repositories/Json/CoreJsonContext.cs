using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using MmmTool.Core.Entities;

namespace MmmTool.Core.Repositories.Json;

/// <summary>Core の JSON 読み書き用のシリアライザ設定。</summary>
/// <remarks>発行時のトリミングでも動くよう、JSON の読み書きはソース生成で行う。</remarks>
[JsonSerializable(typeof(CliCommandSet))]
[JsonSerializable(typeof(CliSettings))]
[JsonSerializable(typeof(LinkMenu))]
internal sealed partial class CoreJsonContext : JsonSerializerContext
{
    /// <summary>手で読み書きしやすい形（インデントあり・日本語や記号を非エスケープ・コメント可・プロパティ名の大文字小文字を区別しない）の設定。</summary>
    public static CoreJsonContext Readable { get; } = new(new JsonSerializerOptions
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    });
}
