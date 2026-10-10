using MmmSdk.Core.Components.Features;
using MmmSdk.Core.Components.Storage;
using MmmTool.WorkItems.Core;

namespace MmmTool.WorkItems;

/// <summary>作業リストの起動時の準備 (保存先からの読み込み)</summary>
/// <param name="workItems">作業リストの読み書き</param>
public sealed class WorkItemStartup(WorkItemService workItems) : IStartupTask
{
    /// <inheritdoc />
    public async Task StartAsync()
    {
        try
        {
            await workItems.LoadAsync();
        }
        catch (DataFileException)
        {
            // 失敗はサービスに残り、画面を開いたときに知らせる (開くたびに読み直す)
        }
    }
}
