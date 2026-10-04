using System.Text;
using System.Text.RegularExpressions;

namespace MmmTool.Core.CliAssist;

/// <summary>
/// 送信テキストの組み立てと表示用の整形。
/// </summary>
public static partial class SendText
{
    /// <summary>本文があるときの、添付ファイルを参照させる指示文</summary>
    /// <remarks>
    /// 添付には元の場所のファイル（ドロップ・貼り付けしたファイル）も含まれる。
    /// 「直して」と頼まれたときは直してよいので、変更を一律には禁じず、本文の指示に従わせる。
    /// </remarks>
    private const string AttachmentInstructionWithBody =
        "上記の指示に関連して、次のファイルも参照してください。バイナリ形式でも、拡張子から判断して読み取れるものは内容を読み取ってください。"
        + "これらのファイルを変更するのは、上記の指示で求められたときだけにしてください。";

    /// <summary>本文が無いときの、添付ファイルを参照させる指示文</summary>
    /// <remarks>指す先の「上記の指示」が無いので、本文があるときとは言い回しを変える。</remarks>
    private const string AttachmentInstructionWithoutBody =
        "次のファイルを参照してください。バイナリ形式でも、拡張子から判断して読み取れるものは内容を読み取ってください。"
        + "これらのファイルを変更するのは、指示で求められたときだけにしてください。";

    /// <summary>入力欄の本文に、添付ファイルの指示文とパスを付け足す</summary>
    /// <param name="text">入力欄の本文</param>
    /// <param name="attachmentPaths">添付ファイルの絶対パス</param>
    /// <returns>送信するテキスト</returns>
    /// <remarks>指示文と、各ファイルの絶対パス（1 行ずつ）を付ける。本文が空白だけのときは、本文が無いものとして扱う。</remarks>
    public static string Compose(string text, IReadOnlyList<string> attachmentPaths)
    {
        var body = text.TrimEnd('\r', '\n');
        if (attachmentPaths.Count == 0)
        {
            return body;
        }

        var builder = new StringBuilder();
        if (string.IsNullOrWhiteSpace(body))
        {
            builder.Append(AttachmentInstructionWithoutBody);
        }
        else
        {
            builder.Append(body).Append("\n\n").Append(AttachmentInstructionWithBody);
        }
        foreach (var path in attachmentPaths)
        {
            builder.Append('\n').Append(path);
        }
        return builder.ToString();
    }

    /// <summary>一覧表示用に 1 行へ整形する</summary>
    /// <param name="text">元のテキスト</param>
    /// <returns>1 行に整形したテキスト</returns>
    /// <remarks>改行・連続する空白を空白 1 個に畳む。</remarks>
    public static string ToSingleLine(string text) => Whitespace().Replace(text.Trim(), " ");

    /// <summary>連続する空白に一致する正規表現</summary>
    /// <returns>連続する空白に一致する正規表現</returns>
    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
