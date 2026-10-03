using MmmTool.Core.Entities;

namespace MmmTool.ViewModels;

/// <summary>定型コマンドのタブの種類</summary>
public enum CommandCategory
{
    /// <summary>シェルで打つコマンド（AI エージェント起動前）。</summary>
    Terminal,

    /// <summary>AI エージェントのセッション内で打つコマンド（起動後）。</summary>
    Session,
}

/// <summary>コマンドを送ったあとにフォーカスを移す先。</summary>
public enum FocusTarget
{
    /// <summary>ターミナル</summary>
    Terminal,

    /// <summary>送信欄の入力欄。</summary>
    Input,
}

/// <summary>ツリーの要素の種類</summary>
public enum CommandItemKind
{
    /// <summary>子を持つ中間ノード。</summary>
    Group,

    /// <summary>コマンド文字列をターミナルへ送る葉。</summary>
    Command,

    /// <summary>作業ディレクトリ変更ダイアログを開く葉</summary>
    /// <remarks>コード側で固定追加する。</remarks>
    ChangeDirectory,
}

/// <summary>
/// 定型コマンドツリーの表示用の 1 要素。
/// </summary>
public sealed class CommandTreeItem
{
    /// <summary>要素を作る</summary>
    /// <param name="label">表示名</param>
    /// <param name="kind">種類</param>
    /// <param name="command">ターミナルへ送るコマンド文字列</param>
    /// <param name="children">子要素</param>
    /// <param name="switchTo">コマンドを送ったあとに切り替えるタブ</param>
    /// <param name="focus">コマンドを送ったあとにフォーカスを移す先</param>
    /// <remarks>生成は <see cref="From"/> などから行う。</remarks>
    private CommandTreeItem(
        string label,
        CommandItemKind kind,
        string? command,
        IReadOnlyList<CommandTreeItem> children,
        CommandCategory? switchTo = null,
        FocusTarget? focus = null)
    {
        Label = label;
        Kind = kind;
        Command = command;
        Children = children;
        SwitchTo = switchTo;
        Focus = focus;
    }

    /// <summary>表示名</summary>
    public string Label { get; }

    /// <summary>種類</summary>
    public CommandItemKind Kind { get; }

    /// <summary>ターミナルへ送るコマンド文字列（葉のとき）</summary>
    public string? Command { get; }

    /// <summary>コマンドを送ったあとに切り替えるタブ</summary>
    /// <remarks>null なら切り替えない。</remarks>
    public CommandCategory? SwitchTo { get; }

    /// <summary>コマンドを送ったあとにフォーカスを移す先</summary>
    /// <remarks>null なら移さない。</remarks>
    public FocusTarget? Focus { get; }

    /// <summary>子要素</summary>
    public IReadOnlyList<CommandTreeItem> Children { get; }

    /// <summary>種類のアイコン</summary>
    public string Glyph => Kind switch
    {
        CommandItemKind.Group => "",
        CommandItemKind.ChangeDirectory => "",
        _ => "",
    };

    /// <summary>ツールチップ（送るコマンド文字列）。</summary>
    public string? ToolTip => Command;

    /// <summary>作業ディレクトリ変更の項目を作る</summary>
    /// <returns>作業ディレクトリ変更の要素</returns>
    public static CommandTreeItem ChangeDirectory() => new("作業ディレクトリ変更", CommandItemKind.ChangeDirectory, null, []);

    /// <summary>定義から表示用の要素を作る</summary>
    /// <param name="node">定型コマンドの定義の 1 要素</param>
    /// <returns>表示用の要素</returns>
    public static CommandTreeItem From(CliCommandNode node)
    {
        // 子を持てば中間ノード（コマンドより子を優先）、無ければコマンドを送る葉
        var children = node.Children?.Select(From).ToList() ?? [];
        return children.Count > 0 || string.IsNullOrWhiteSpace(node.Command)
            ? new CommandTreeItem(node.Label, CommandItemKind.Group, null, children)
            : new CommandTreeItem(node.Label, CommandItemKind.Command, node.Command, [],
                Parse<CommandCategory>(node.SwitchTo), Parse<FocusTarget>(node.Focus));
    }

    /// <summary>文字列を列挙値に変換する。変換できなければ null</summary>
    /// <typeparam name="T">変換先の列挙型</typeparam>
    /// <param name="value">変換する文字列</param>
    /// <returns>変換した列挙値。変換できなければ null</returns>
    /// <remarks>手で編集した JSON なので、大文字小文字は区別せず、知らない値は無視する（何もしない）。</remarks>
    private static T? Parse<T>(string? value) where T : struct, Enum
        => !string.IsNullOrWhiteSpace(value) && Enum.TryParse<T>(value.Trim(), ignoreCase: true, out var result) && Enum.IsDefined(result)
            ? result
            : null;
}
