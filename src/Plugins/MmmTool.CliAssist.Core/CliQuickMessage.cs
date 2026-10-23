namespace MmmTool.CliAssist.Core;

/// <summary>入力欄が空のときに出す、よく使う文またはグループ (Data/CliQuickMessages.json の 1 件)</summary>
/// <remarks>
/// 手で書いても、編集画面で書いてもよいファイルの項目。
/// <see cref="Items"/> があればグループで、無ければ文。グループは 1 段だけで、グループの中のグループは使わない。
/// </remarks>
public sealed class CliQuickMessage
{
    /// <summary>チップに表示する名前</summary>
    /// <remarks>文は、省略すると <see cref="Text"/> の 1 行目。グループは、グループ名。</remarks>
    public string? Label { get; set; }

    /// <summary>入力欄に入れる文 (文のとき)</summary>
    public string? Text { get; set; }

    /// <summary>グループの中の文 (グループのとき)</summary>
    /// <remarks><see cref="Text"/> があっても、<see cref="Items"/> があればグループとして扱う。空のリストも、文が 0 件のグループ。</remarks>
    public List<CliQuickMessage>? Items { get; set; }
}
