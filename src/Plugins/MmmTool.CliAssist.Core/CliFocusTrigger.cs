namespace MmmTool.CliAssist.Core;

/// <summary>ターミナルの出力に出たら、ターミナルへフォーカスを移す文字列 (Data/CliFocusTriggers.json の 1 件)</summary>
/// <remarks>編集画面で書いても、手で書いてもよいファイルの項目。</remarks>
public sealed class CliFocusTrigger
{
    /// <summary>出力に出る文字列</summary>
    public string? Text { get; set; }
}
