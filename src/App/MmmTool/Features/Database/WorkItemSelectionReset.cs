using MmmSdk.Core.Components.Settings;
using MmmTool.Data.Connection;
using MmmTool.WorkItems.Core;

namespace MmmTool.Features.Database;

/// <summary>
/// 保存先のモード (ローカル・サーバー)が変わったときに、作業管理の表示しない案件の番号を初期化する。
/// </summary>
/// <param name="settings">この PC の設定</param>
/// <remarks>案件の番号は、ローカルの SQLite と SQL Server で別の連番なので、モードが変わると合わなくなる。ユーザー設定なので、消す (全部の案件が表示に戻る)。</remarks>
public sealed class WorkItemSelectionReset(ISettingsStore settings) : IDatabaseModeChangeHandler
{
    /// <inheritdoc />
    public async Task OnModeChangedAsync(CancellationToken cancellationToken = default)
        => await settings.RemoveAsync(WorkItemSettingKeys.HiddenProjects, cancellationToken);
}
