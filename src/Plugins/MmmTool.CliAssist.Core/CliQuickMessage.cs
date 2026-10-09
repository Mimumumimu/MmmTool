namespace MmmTool.CliAssist.Core;

/// <summary>入力欄が空のときに出す、よく使う文 (Data/CliQuickMessages.json の 1 件)</summary>
/// <remarks>手で書いても、編集画面で書いてもよいファイルの項目。</remarks>
public sealed class CliQuickMessage
{
    /// <summary>チップに表示する名前 (省略すると、<see cref="Text"/> の 1 行目)</summary>
    public string? Label { get; set; }

    /// <summary>入力欄に入れる文</summary>
    public string? Text { get; set; }
}
