using System.Windows.Input;
using MmmSdk.Core.Utilities;
using MmmTool.Backlog.Core;

namespace MmmTool.Backlog.Main;

/// <summary>
/// 共有ファイルの一覧の 1 行
/// </summary>
/// <param name="Source">元の共有ファイル</param>
/// <param name="DownloadCommand">ダウンロードのコマンド (引数はこの行)</param>
public sealed record BacklogFileItem(BacklogSharedFile Source, ICommand DownloadCommand)
{
    /// <summary>ファイル名</summary>
    public string Name => Source.Name;

    /// <summary>大きさの表記</summary>
    public string SizeText => FileSizeFormatter.Format(Source.Size ?? 0);

    /// <summary>更新日時の表記 (この PC のタイムゾーン)。応答に無いときは空</summary>
    public string UpdatedText => Source.Updated?.ToLocalTime().ToString("yyyy/MM/dd HH:mm") ?? "";
}
