using Microsoft.Extensions.DependencyInjection;
using MmmSdk.Core.Components.Features;
using MmmSdk.WinUI.Components.Pages;
using MmmTool.Notes.Core;
using MmmTool.Notes.Edit;
using MmmTool.Notes.Main;

namespace MmmTool.Notes;

/// <summary>ノートの入口 (ページ・入力ウィンドウを登録する)</summary>
/// <remarks>
/// 保存先 (<see cref="INoteRepository"/>)は、ここでは登録しない。設定でローカルの SQLite か DB の SQL Server かを選ぶのは、両方を参照できるホストの役目
/// (ホストの <c>AddStorage</c>。機能が増えても、選ぶ処理は 1 か所)。
/// </remarks>
public sealed class NotesPlugin : IFeaturePlugin
{
    /// <inheritdoc />
    public void Register(IServiceCollection services)
    {
        services.AddSingleton<INoteBodyFormatter, NoteBodyFormatter>();
        services.AddSingleton<NotePageBuilder>();
        services.AddSingleton<NoteService>();

        services.AddFeature(NotesFeature.Key, NotesFeature.DisplayName);

        services.AddSingleton<INoteDialogService, NoteDialogService>();

        // 閉じたウィンドウは再表示できないので、開くたびに作る
        services.AddTransient<NoteEditWindow>();
        services.AddTransient<NoteEditViewModel>();

        services.AddTransient<NotesViewModel>();

        // アイコンは Segoe Fluent Icons の QuickNote (E70B。付箋とペン)
        services.AddNavigationPage<NotesPage>(NotesFeature.DisplayName, "", NavigationArea.Top, NotesFeature.Key);
    }
}
