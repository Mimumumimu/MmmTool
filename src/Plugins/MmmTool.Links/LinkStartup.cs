using MmmSdk.Core.Components.Features;
using MmmSdk.Core.Components.Storage;
using MmmTool.Links.Core;

namespace MmmTool.Links;

/// <summary>リンクの起動時の準備 (トレイのリンクメニュー用の読み込み)</summary>
/// <param name="linkMenu">リンクメニューの読み書き</param>
public sealed class LinkStartup(LinkMenuService linkMenu) : IStartupTask
{
    /// <inheritdoc />
    public async Task StartAsync()
    {
        try
        {
            await linkMenu.LoadAsync();
        }
        catch (DataFileException)
        {
            // 失敗はサービスに残り、トレイのメニューとリンク画面 (開いたときに読み直す)で知らせる
        }
    }
}
