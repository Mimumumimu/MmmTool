namespace MmmTool.CliAssist.Core;

/// <summary>
/// CLI補助の利用状態 (Data/CliSettings.json)。DB には載せないローカル専用の設定。
/// </summary>
public sealed class CliSettings
{
    /// <summary>最後に移動した作業ディレクトリ</summary>
    /// <remarks>次回起動時のシェルの開始位置にも使う。</remarks>
    public string? LastDirectory { get; set; }

    /// <summary>作業ディレクトリの履歴 (先頭が最新)。</summary>
    public List<string> DirectoryHistory { get; set; } = [];
}
