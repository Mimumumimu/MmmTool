using Microsoft.UI.Dispatching;
using MmmSdk.Core.Components.Features;
using MmmSdk.Core.Components.Notifications;
using MmmSdk.WinUI.Components.Notifications;
using MmmSdk.WinUI.Components.Pages;
using MmmTool.Notes.Core;
using MmmTool.Notes.Main;
using MmmTool.NotificationChannels.Core;

namespace MmmTool.Notes;

/// <summary>他の人の作成・更新の通知の起動時の準備 (ノートの更新の監視の開始)</summary>
/// <param name="monitor">ノートの更新の監視</param>
/// <param name="notifications">通知ダイアログの表示</param>
/// <param name="navigation">サイドバーのページを開く口</param>
/// <param name="pages">作ったページ (開いているノートのページの表示を、そのノートが有効な日に合わせるため)</param>
/// <param name="features">機能のオン・オフ</param>
/// <param name="notes">ノートの読み書き (今日を決める)</param>
/// <remarks>
/// 監視はスレッドプールのスレッドから知らせてくるので、UI スレッドに切り替えて通知ダイアログに節を出す。
/// 今日実施のノートの節 (<see cref="NotesEventStartup"/>)とは別の節で、一方を押しても他方は閉じない。
/// 節をクリックしたら、サイドバーでノートのページを開き、表示する日を、先頭の項目のノートが有効な日にする。
/// </remarks>
public sealed class NoteChangeStartup(
    NoteChangeMonitor monitor,
    INotificationDialogService notifications,
    INavigationService navigation,
    IPageCache pages,
    IFeatureStatus features,
    NoteService notes) : IStartupTask
{
    /// <summary>通知ダイアログの節の、送り元のキー。今日実施の節 (<c>Notes</c>)と分ける</summary>
    private const string NotificationSourceKey = "note-change";

    /// <summary>通知ダイアログの節の並びの値。サイドバーの並び (ノート 2)と同じ</summary>
    private const int NotificationOrder = 2;

    /// <inheritdoc />
    /// <remarks>UI スレッドから呼ぶ。通知を出すときに戻る先として、呼んだスレッドのディスパッチャーを使うため。</remarks>
    public Task StartAsync()
    {
        var dispatcher = DispatcherQueue.GetForCurrentThread();
        monitor.Start(
            () => features.IsEnabled(NotesFeature.Key),
            found => dispatcher.TryEnqueue(() => Show(found, dispatcher)));
        return Task.CompletedTask;
    }

    /// <summary>通知ダイアログに、他の人の作成・更新を並べた節を出す</summary>
    /// <param name="found">他の人の作成・更新</param>
    /// <param name="dispatcher">UI スレッドのディスパッチャー</param>
    private void Show(IReadOnlyList<NoteNotice> found, DispatcherQueue dispatcher)
    {
        var first = found[0];
        notifications.Show(new NotificationSection(
            NotificationSourceKey,
            NotificationOrder,
            "ノートの更新",
            [.. found.Select(notice => new NotificationItem(NoteNotificationText.Heading(notice.EditorName), Detail: NoteNotificationText.Body(notice.Change.Title, notice.IsCreated)))],
            OnClicked: () => dispatcher.TryEnqueue(
                // 通知ウィンドウが閉じている最中に別のウィンドウを操作しないよう、閉じ終わってから開く
                DispatcherQueuePriority.Low, () => OpenNotesPage(first))));
    }

    /// <summary>ノートのページを開いて、表示する日を、そのノートが有効な日にする</summary>
    /// <param name="notice">開く先のノートの知らせ</param>
    private void OpenNotesPage(NoteNotice notice)
    {
        if (navigation.Open<NotesPage>())
        {
            pages.GetCreatedPage<NotesPage>()?.ViewModel.ShowDay(notice.Change.DayToOpen(notes.Today));
        }
    }
}
