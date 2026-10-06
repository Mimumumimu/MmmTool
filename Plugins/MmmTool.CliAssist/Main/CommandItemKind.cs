namespace MmmTool.CliAssist.Main;

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
