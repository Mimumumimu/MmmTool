using System.Text.Json.Serialization;

namespace MmmTool.Core.ClipboardTransfer.Json;

/// <summary>クリップボード転送の JSON 読み書き用のシリアライザ設定</summary>
/// <remarks>
/// 形式を固定するため、.NET 標準の既定の設定のまま使う (<c>Default</c>)。
/// 保存ファイルのように手で読み書きしやすい形 (SDK の ReadableJsonOptions。キーが camelCase になる)にすると、形式が変わってしまうので、使わない。
/// 発行時のトリミングでも動くよう、JSON の読み書きはソース生成で行う。
/// </remarks>
[JsonSerializable(typeof(List<TransferFile>))]
internal sealed partial class TransferJsonContext : JsonSerializerContext;
