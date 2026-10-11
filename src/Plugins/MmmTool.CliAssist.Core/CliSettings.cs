namespace MmmTool.CliAssist.Core;

/// <summary>
/// CLI補助の利用状態 (Data/CliSettings.json)。DB には載せないローカル専用の設定。
/// </summary>
public sealed class CliSettings
{
    /// <summary>最後に移動した作業ディレクトリ</summary>
    /// <remarks>戻せるタブが無いときの、次回起動時のシェルの開始位置に使う。</remarks>
    public string? LastDirectory { get; set; }

    /// <summary>作業ディレクトリの履歴 (先頭が最新)。</summary>
    public List<string> DirectoryHistory { get; set; } = [];

    /// <summary>開いているタブの作業ディレクトリ (タブの並び順)</summary>
    /// <remarks>次回起動時にタブを戻すのに使う。シェルや CLI の状態は含まない。</remarks>
    public List<string> Tabs { get; set; } = [];

    /// <summary>選んでいたタブの作業ディレクトリ</summary>
    public string? SelectedTab { get; set; }
}
