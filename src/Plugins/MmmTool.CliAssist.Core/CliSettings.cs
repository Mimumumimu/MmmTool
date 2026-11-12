namespace MmmTool.CliAssist.Core;

/// <summary>CLI補助の利用状態で、Data/CliSettings.json に保存する</summary>
/// <remarks>DB には載せないローカル専用の設定</remarks>
public sealed class CliSettings
{
    /// <summary>最後に移動した作業ディレクトリ</summary>
    /// <remarks>戻せるタブが無いときの次回起動時のシェルの開始位置に使う</remarks>
    public string? LastDirectory { get; set; }

    /// <summary>作業ディレクトリの履歴で先頭が最新</summary>
    public List<string> DirectoryHistory { get; set; } = [];

    /// <summary>開いているタブの作業ディレクトリ</summary>
    /// <remarks>
    /// 次回起動時にタブを戻すのに使う
    /// シェルや CLI の状態は含まない
    /// </remarks>
    public List<string> Tabs { get; set; } = [];

    /// <summary>選んでいたタブの作業ディレクトリ</summary>
    public string? SelectedTab { get; set; }
}
