using System.Text.Json;
using System.Text.Json.Serialization;
using MmmToolWeb.Features.Client;

namespace MmmToolWeb.Notes.Client;

/// <summary>
/// ノートの API の JSON を読み書きする、ソース生成の設定。サーバーとブラウザが同じものを使う。
/// </summary>
/// <remarks>
/// ブラウザの発行では、使っていない型が削られるので、型を書いて調べる方式 (リフレクション)に頼らない。
/// </remarks>
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(NoteListResponse))]
[JsonSerializable(typeof(NoteSaveRequest))]
[JsonSerializable(typeof(NoteDeletedRequest))]
[JsonSerializable(typeof(DayContentSourceListResponse))]
[JsonSerializable(typeof(DayContentFramesResponse))]
[JsonSerializable(typeof(ErrorResponse))]
public sealed partial class NotesJsonContext : JsonSerializerContext;
