using System.Text.Json.Serialization;

namespace MmmToolWeb.Features.Client;

/// <summary>
/// 使えない機能の一覧 (<see cref="FeatureAvailability"/>)を読むための、ソース生成の設定。
/// </summary>
[JsonSerializable(typeof(string[]))]
internal sealed partial class FeatureAvailabilityJsonContext : JsonSerializerContext;
