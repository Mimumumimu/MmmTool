namespace MmmTool.Core.CliAssist;

/// <summary>コマンドを送ったあとにフォーカスを移す先。</summary>
public enum FocusTarget
{
    /// <summary>ターミナル</summary>
    Terminal,

    /// <summary>送信欄の入力欄。</summary>
    Input,
}
