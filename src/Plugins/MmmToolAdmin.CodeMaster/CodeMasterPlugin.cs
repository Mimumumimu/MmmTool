using Microsoft.Extensions.DependencyInjection;
using MmmSdk.Core.Components.Features;
using MmmSdk.WinUI.Components.Pages;
using MmmTool.CodeMaster;
using MmmTool.CodeMaster.Settings;
using MmmTool.WorkItems.Core;

namespace MmmToolAdmin.CodeMaster;

/// <summary>選択肢の入口で、編集できる区分とページを登録する</summary>
/// <remarks>
/// コードマスタの保存先は、ホストが DB の実装を登録する。編集の画面は MmmTool と共有する。
/// 編集できる区分は、ここに 1 行ずつ足す。機能が増えたら、その機能の Core が持つ区分の定義を足す。
/// </remarks>
public sealed class CodeMasterPlugin : IFeaturePlugin
{
    /// <inheritdoc />
    public void Register(IServiceCollection services)
    {
        // MmmToolAdmin の機能は、オフにできず、オン・オフの画面も持たない。キーは、サイドバーの並びのために使う
        services.AddAlwaysOnFeature(CodeMasterFeature.Key, CodeMasterFeature.DisplayName);

        services.AddCodeMasterEditing();
        services.AddTransient<CodeMasterSettingsControl>();

        // 編集できる区分。作業管理の進捗度の名前は、MmmTool の設定ページと同じ定義を使う。開いている画面を持たないので、保存後の処理は無い
        services.AddCodeCategoryEditor(_ => WorkCodes.ProgressNameEditor(featureKey: null));

        // アイコンは Segoe Fluent Icons の Tag で、E8EC
        services.AddNavigationPage<CodeMasterPage>(CodeMasterFeature.DisplayName, "", NavigationArea.Top, CodeMasterFeature.Key);
    }
}
