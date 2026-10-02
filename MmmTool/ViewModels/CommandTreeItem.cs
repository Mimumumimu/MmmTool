using MmmTool.Core.Entities;

namespace MmmTool.ViewModels;

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
    Terminal,

    /// <summary>送信欄の入力欄。</summary>
    Input,
}

public enum CommandItemKind
{
    /// <summary>子を持つ中間ノード。</summary>
    Group,

    /// <summary>コマンド文字列をターミナルへ送る葉。</summary>
    Command,

    /// <summary>作業ディレクトリ変更ダイアログを開く葉（コード側で固定追加する）。</summary>
    ChangeDirectory,
}

/// <summary>
/// 定型コマンドツリーの表示用の 1 要素。
/// </summary>
public sealed class CommandTreeItem
{
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

    public string Label { get; }

    public CommandItemKind Kind { get; }

    public string? Command { get; }

    /// <summary>コマンドを送ったあとに切り替えるタブ。null なら切り替えない。</summary>
    public CommandCategory? SwitchTo { get; }

    /// <summary>コマンドを送ったあとにフォーカスを移す先。null なら移さない。</summary>
    public FocusTarget? Focus { get; }

    public IReadOnlyList<CommandTreeItem> Children { get; }

    public string Glyph => Kind switch
    {
        CommandItemKind.Group => "",
        CommandItemKind.ChangeDirectory => "",
        _ => "",
    };

    /// <summary>ツールチップ（送るコマンド文字列）。</summary>
    public string? ToolTip => Command;

    public static CommandTreeItem ChangeDirectory() => new("作業ディレクトリ変更", CommandItemKind.ChangeDirectory, null, []);

    public static CommandTreeItem From(CliCommandNode node)
    {
        // 子を持てば中間ノード（コマンドより子を優先）、無ければコマンドを送る葉
        var children = node.Children?.Select(From).ToList() ?? [];
        return children.Count > 0 || string.IsNullOrWhiteSpace(node.Command)
            ? new CommandTreeItem(node.Label, CommandItemKind.Group, null, children)
            : new CommandTreeItem(node.Label, CommandItemKind.Command, node.Command, [],
                Parse<CommandCategory>(node.SwitchTo), Parse<FocusTarget>(node.Focus));
    }

    // 手で編集した JSON なので、大文字小文字は区別せず、知らない値は無視する（何もしない）
    private static T? Parse<T>(string? value) where T : struct, Enum
        => !string.IsNullOrWhiteSpace(value) && Enum.TryParse<T>(value.Trim(), ignoreCase: true, out var result) && Enum.IsDefined(result)
            ? result
            : null;
}
