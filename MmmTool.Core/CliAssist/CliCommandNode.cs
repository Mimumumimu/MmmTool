namespace MmmTool.Core.CliAssist;

/// <summary>
/// 定型コマンドツリーの 1 要素。子を持てば中間ノード、コマンドを持てば葉。
/// </summary>
public sealed class CliCommandNode
{
    /// <summary>表示名</summary>
    public string Label { get; set; } = "";

    /// <summary>ターミナルへ送るコマンド文字列（葉のとき）。</summary>
    public string? Command { get; set; }

    /// <summary>コマンドを送ったあとに切り替える左ペインのタブ（"shell" または "session"）</summary>
    /// <remarks>省略すると切り替えない。</remarks>
    public string? SwitchTo { get; set; }

    /// <summary>コマンドを送ったあとにフォーカスを移す先（"terminal" または "input"＝送信欄）</summary>
    /// <remarks>省略すると移さない。</remarks>
    public string? Focus { get; set; }

    /// <summary>子要素（中間ノードのとき）。</summary>
    public List<CliCommandNode>? Children { get; set; }

    /// <summary>列挙値を、JSON に書く文字列にする</summary>
    /// <typeparam name="T">列挙型（<see cref="CommandCategory"/> / <see cref="FocusTarget"/> / <see cref="CliEnvironment"/>）</typeparam>
    /// <param name="value">列挙値</param>
    /// <returns>JSON に書く文字列（名前の小文字。例: <c>"session"</c>）</returns>
    /// <remarks>文字列との変換は、読む側（<see cref="Parse{T}"/>）とこの 1 か所に集める。</remarks>
    public static string ToJsonValue<T>(T value) where T : struct, Enum => value.ToString().ToLowerInvariant();

    /// <summary><see cref="SwitchTo"/> を列挙値にする</summary>
    /// <returns>切り替えるタブ。省略・不正な値なら null</returns>
    public CommandCategory? GetSwitchTo() => Parse<CommandCategory>(SwitchTo);

    /// <summary><see cref="Focus"/> を列挙値にする</summary>
    /// <returns>フォーカスを移す先。省略・不正な値なら null</returns>
    public FocusTarget? GetFocus() => Parse<FocusTarget>(Focus);

    /// <summary>文字列を列挙値に変換する。変換できなければ null</summary>
    /// <typeparam name="T">変換先の列挙型</typeparam>
    /// <param name="value">変換する文字列</param>
    /// <returns>変換した列挙値。変換できなければ null</returns>
    /// <remarks>手で編集した JSON なので、大文字小文字は区別せず、知らない値は無視する（何もしない）。<see cref="CliCommandSet"/> の項目の変換にも使う。</remarks>
    internal static T? Parse<T>(string? value) where T : struct, Enum
        => !string.IsNullOrWhiteSpace(value) && Enum.TryParse<T>(value.Trim(), ignoreCase: true, out var result) && Enum.IsDefined(result)
            ? result
            : null;
}
