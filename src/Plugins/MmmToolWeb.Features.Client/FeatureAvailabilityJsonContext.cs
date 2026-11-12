using System.Text.Json.Serialization;

namespace MmmToolWeb.Features.Client;

/// <summary>使えない機能の一覧を読むためのソース生成の設定</summary>
/// <remarks><see cref="FeatureAvailability"/></remarks>
[JsonSerializable(typeof(string[]))]
internal sealed partial class FeatureAvailabilityJsonContext : JsonSerializerContext;
