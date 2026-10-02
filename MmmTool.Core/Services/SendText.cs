using System.Text;
using System.Text.RegularExpressions;

namespace MmmTool.Core.Services;

/// <summary>
/// 送信テキストの組み立てと表示用の整形。
/// </summary>
public static partial class SendText
{
    private const string AttachmentInstruction =
        "上記の指示に関連して、次のファイルも参照してください。バイナリ形式でも、拡張子から判断して読み取れるものは内容を読み取ってください。";

    /// <summary>入力欄の本文に、添付ファイルを参照させる指示文と各ファイルの絶対パス（1 行ずつ）を付け足す。</summary>
    public static string Compose(string text, IReadOnlyList<string> attachmentPaths)
    {
        var body = text.TrimEnd('\r', '\n');
        if (attachmentPaths.Count == 0)
        {
            return body;
        }

        var builder = new StringBuilder();
        if (body.Length > 0)
        {
            builder.Append(body).Append("\n\n");
        }
        builder.Append(AttachmentInstruction);
        foreach (var path in attachmentPaths)
        {
            builder.Append('\n').Append(path);
        }
        return builder.ToString();
    }

    /// <summary>一覧表示用に 1 行へ整形する（改行・連続する空白を空白 1 個に畳む）。</summary>
    public static string ToSingleLine(string text) => Whitespace().Replace(text.Trim(), " ");

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
