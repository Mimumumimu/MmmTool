namespace MmmTool.Core.ClipboardTransfer;

/// <summary>クリップボード転送で渡す、ファイル 1 件分のデータ</summary>
/// <param name="FileName">ファイル名 (フォルダーは含まない)</param>
/// <param name="Base64Data">ファイルの内容の Base64 文字列</param>
/// <param name="CreationTime">作成日時</param>
/// <param name="LastWriteTime">更新日時</param>
/// <remarks>
/// JSON の形式は固定 (プロパティ名は PascalCase のまま。<see cref="Json.TransferJsonContext"/>)。変えると、すでにクリップボードに載せた・書き出した JSON を読めなくなる。
/// 日時は ISO 8601 (例: 2026-10-05T12:00:00+09:00)で書き、時差つきのまま読めるよう <see cref="DateTimeOffset"/> で持つ。
/// </remarks>
public sealed record TransferFile(string FileName, string Base64Data, DateTimeOffset CreationTime, DateTimeOffset LastWriteTime);
