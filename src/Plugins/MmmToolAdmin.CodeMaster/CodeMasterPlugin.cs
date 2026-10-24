using Microsoft.Extensions.DependencyInjection;
using MmmSdk.Core.Components.Features;
using MmmSdk.WinUI.Components.Pages;
using MmmTool.CodeMaster;
using MmmTool.CodeMaster.Settings;
using MmmTool.WorkItems.Core;

namespace MmmToolAdmin.CodeMaster;

/// <summary>共通設定に並ぶ「選択肢」の部品と、編集できる区分を登録する</summary>
/// <remarks>
/// コードマスタの保存先は、ホストが DB の実装を登録する。編集の画面は MmmTool と共有する。
/// 編集できる区分は、ここに 1 行ずつ足す。機能が増えたら、その機能の Core が持つ区分の定義を足す。
/// </remarks>
public sealed class CodeMasterPlugin : IFeaturePlugin
{
    /// <inheritdoc />
    public void Register(IServiceCollection services)
    {
        services.AddCodeMasterEditing();

        // 共通設定のページに並ぶ部品。サイドバーの項目とページは、MmmToolAdmin.CommonSettings が持つ
        services.AddSettingsSection<CodeMasterSettingsControl>();

        // 編集できる区分。作業管理の進捗度の名前は、MmmTool の設定ページと同じ定義を使う。開いている画面を持たないので、保存後の処理は無い
        services.AddCodeCategoryEditor(_ => WorkCodes.ProgressNameEditor(featureKey: null));
    }
}
