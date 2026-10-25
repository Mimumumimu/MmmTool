using Microsoft.UI.Dispatching;
using MmmSdk.Core.Components.Features;
using MmmSdk.Core.Components.Notifications;
using MmmSdk.WinUI.Components.Notifications;
using MmmSdk.WinUI.Components.Pages;
using MmmTool.Notes.Core;
using MmmTool.Notes.Main;

namespace MmmTool.Notes;

/// <summary>ノートの起動時の準備 (今日が実施日のノートの監視の開始)</summary>
/// <param name="monitor">実施日の監視</param>
/// <param name="notifications">通知ダイアログの表示</param>
/// <param name="navigation">サイドバーのページを開く口</param>
/// <param name="pages">作ったページ (開いているノートのページの表示を、今日に合わせるため)</param>
/// <param name="features">機能のオン・オフ</param>
/// <remarks>
/// 監視はスレッドプールのスレッドから知らせてくるので、UI スレッドに切り替えて通知ダイアログに節を出す。
/// 節をクリックしたら、サイドバーでノートのページを開き、表示する日を今日にする。
/// </remarks>
public sealed class NotesEventStartup(
    NoteEventMonitor monitor,
    INotificationDialogService notifications,
    INavigationService navigation,
    IPageCache pages,
    IFeatureStatus features) : IStartupTask
{
    /// <summary>通知ダイアログの節の、送り元のキー</summary>
    private const string NotificationSourceKey = "Notes";

    /// <summary>通知ダイアログの節の並びの値。サイドバーの並び (ノート 2)と同じで、リマインダー (4)の上に固定する</summary>
    private const int NotificationOrder = 2;

    /// <inheritdoc />
    /// <remarks>UI スレッドから呼ぶ。通知を出すときに戻る先として、呼んだスレッドのディスパッチャーを使うため。</remarks>
    public Task StartAsync()
    {
        var dispatcher = DispatcherQueue.GetForCurrentThread();
        monitor.Start(
            () => features.IsEnabled(NotesFeature.Key),
            notes => dispatcher.TryEnqueue(() => Show(notes, dispatcher)));
        return Task.CompletedTask;
    }

    /// <summary>通知ダイアログに、今日が実施日のノートのタイトルを並べた節を出す</summary>
    /// <param name="notes">今日が実施日のノート</param>
    /// <param name="dispatcher">UI スレッドのディスパッチャー</param>
    private void Show(IReadOnlyList<Note> notes, DispatcherQueue dispatcher)
        => notifications.Show(new NotificationSection(
            NotificationSourceKey,
            NotificationOrder,
            "今日実施のノート",
            [.. notes.Select(note => new NotificationItem(note.Title))],
            OnClicked: () => dispatcher.TryEnqueue(
                // 通知ウィンドウが閉じている最中に別のウィンドウを操作しないよう、閉じ終わってから開く
                DispatcherQueuePriority.Low, OpenNotesPage)));

    /// <summary>ノートのページを開いて、表示する日を今日にする</summary>
    private void OpenNotesPage()
    {
        if (navigation.Open<NotesPage>())
        {
            pages.GetCreatedPage<NotesPage>()?.ViewModel.ShowToday();
        }
    }
}
