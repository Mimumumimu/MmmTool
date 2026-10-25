using Microsoft.Extensions.DependencyInjection;
using MmmSdk.WinUI.Components.Pages;
using MmmTool.CodeMaster.Core;
using MmmTool.CodeMaster.Edit;
using MmmTool.CodeMaster.Settings;

namespace MmmTool.CodeMaster;

/// <summary>
/// コードマスタの名前を編集する画面 (設定ページの「選択肢」と編集ウィンドウ)を DI に登録する。
/// </summary>
public static class CodeMasterServiceCollectionExtensions
{
    /// <summary>設定ページの「選択肢」と編集ウィンドウを登録する</summary>
    /// <param name="services">登録先のサービスコレクション</param>
    /// <param name="order">設定ページでの並び順の値</param>
    /// <returns>登録先のサービスコレクション</returns>
    /// <remarks>
    /// コードマスタの保存先 (<see cref="ICodeMasterRepository"/>)は、ホストが設定で選んで登録する。
    /// 出す区分は、各機能が <see cref="AddCodeCategoryEditor"/> で登録する。この画面は、特定の機能を知らない。
    /// </remarks>
    public static IServiceCollection AddCodeMasterSettings(this IServiceCollection services, int order)
    {
        services.AddCodeMasterEditing();
        services.AddSettingsSection<CodeMasterSettingsControl>(order: order);
        return services;
    }

    /// <summary>区分を並べる部品の ViewModel と、編集ウィンドウを登録する</summary>
    /// <param name="services">登録先のサービスコレクション</param>
    /// <returns>登録先のサービスコレクション</returns>
    /// <remarks>
    /// 部品 <see cref="CodeMasterSettingsControl"/> 自体は登録しない。設定ページに載せるアプリは <see cref="AddCodeMasterSettings"/> を使い、別のページ (管理ツールの「選択肢」)に置くアプリは、部品を自分で登録する。
    /// </remarks>
    public static IServiceCollection AddCodeMasterEditing(this IServiceCollection services)
    {
        services.AddSingleton<ICodeMasterDialogService, CodeMasterDialogService>();
        services.AddTransient<CodeMasterSettingsViewModel>();

        // 閉じたウィンドウは再表示できないので、開くたびに作る
        services.AddTransient<CodeNameEditWindow>();
        services.AddTransient<CodeNameEditViewModel>();
        return services;
    }

    /// <summary>名前を編集できるコードマスタの区分を登録する</summary>
    /// <param name="services">登録先のサービスコレクション</param>
    /// <param name="create">区分を作る処理 (保存後の後始末に、機能のサービスを使えるよう、DI から受け取る)</param>
    /// <returns>登録先のサービスコレクション</returns>
    public static IServiceCollection AddCodeCategoryEditor(this IServiceCollection services, Func<IServiceProvider, CodeCategoryEditor> create)
        => services.AddSingleton(create);
}
