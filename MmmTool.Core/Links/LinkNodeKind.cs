using System.Text.Json.Serialization;

namespace MmmTool.Core.Links;

/// <summary>リンクメニューの要素の種類</summary>
/// <remarks>JSON では <c>"link"</c> / <c>"folder"</c> / <c>"separator"</c> で書く。</remarks>
[JsonConverter(typeof(JsonStringEnumConverter<LinkNodeKind>))]
public enum LinkNodeKind
{
    /// <summary>リンク（開く先を持つ）</summary>
    [JsonStringEnumMemberName("link")]
    Link,

    /// <summary>子を持てる中間ノード（空でもよい）。</summary>
    [JsonStringEnumMemberName("folder")]
    Folder,

    /// <summary>区切り線</summary>
    [JsonStringEnumMemberName("separator")]
    Separator,
}
