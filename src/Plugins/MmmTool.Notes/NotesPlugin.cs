using Microsoft.Extensions.DependencyInjection;
using MmmSdk.Core.Components.Features;
using MmmSdk.WinUI.Components.Pages;
using MmmTool.CodeMaster.Core;
using MmmTool.Notes.Core;
using MmmTool.Notes.Edit;
using MmmTool.Notes.Main;
using MmmTool.Users.Core;

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

        // 他の機能 (作業管理など)が登録した内容を集めて、カードの上に出す。提供する機能の名前は持たない
        services.AddSingleton<DayContentService>();

        // 管理者が無効にしたユーザー (サーバーモード)には、サイドバー・設定の機能の一覧に出ない
        services.AddFeature(NotesFeature.Key, NotesFeature.DisplayName,
            availableWhen: provider => provider.IsFeatureAllowed(CodeFeatures.Notes));

        // 今日が実施日のノートを、起動時と日付が変わったときに、通知ダイアログで知らせる
        services.AddSingleton<NoteEventMonitor>();
        services.AddStartupTask<NotesEventStartup>(NotesFeature.Key);

        services.AddSingleton<INoteDialogService, NoteDialogService>();

        // 閉じたウィンドウは再表示できないので、開くたびに作る
        services.AddTransient<NoteEditWindow>();
        services.AddTransient<NoteEditViewModel>();

        services.AddTransient<NotesViewModel>();

        // アイコンは Segoe Fluent Icons の QuickNote (E70B。付箋とペン)
        services.AddNavigationPage<NotesPage>(NotesFeature.DisplayName, "", NavigationArea.Top, NotesFeature.Key, canDetach: true);
    }
}
