using Microsoft.UI.Dispatching;
using MmmSdk.Core.Components.Features;
using MmmSdk.Core.Components.Notifications;
using MmmSdk.WinUI.Components.Notifications;
using MmmSdk.WinUI.Components.Pages;
using MmmTool.Notes.Core;
using MmmTool.Notes.Main;
using MmmTool.NotificationChannels.Core;

namespace MmmTool.Notes;

/// <summary>他の人の作成・更新の通知の起動時の準備</summary>
/// <param name="monitor">ノートの更新の監視</param>
/// <param name="notifications">通知ダイアログの表示</param>
/// <param name="navigation">サイドバーのページを開く口</param>
/// <param name="pages">作ったページ (開いているノートのページの表示を、そのノートが有効な日に合わせるため)。</param>
/// <param name="features">機能のオン・オフ</param>
/// <param name="notes">ノートの読み書き (今日を決める)</param>
/// <remarks>
/// 監視はスレッドプールのスレッドから知らせてくるので、UI スレッドに切り替えて通知ダイアログに節を出す。
/// 今日実施のノートの節 (<see cref="NotesEventStartup"/>)とは別の節で、一方を押しても他方は閉じない。
/// 押すまで見ていない知らせはここで持ち、新しい知らせのたびに持っている全部で節を作り直す。
/// 節は更新した人ごとに 1 つの項目にまとめ、その下にノートを新しい順に並べる。
/// 節をクリックしたら、持っている知らせを全部捨て、サイドバーでノートのページを開く。
/// 表示する日はいちばん新しく更新されたノートが有効な日にする。
/// ×で閉じたときも持っている知らせを捨てて、次の通知に前の知らせが復活しないようにする。
/// </remarks>
public sealed class NoteChangeStartup(
    NoteChangeMonitor monitor,
    INotificationDialogService notifications,
    INavigationService navigation,
    IPageCache pages,
    IFeatureStatus features,
    NoteService notes) : IStartupTask
{
    /// <summary>通知ダイアログの節の送り元のキー</summary>
    /// <remarks>今日実施の節と分ける</remarks>
    private const string NotificationSourceKey = "note-change";

    /// <summary>通知ダイアログの節の並びの値</summary>
    /// <remarks>サイドバーの並びと同じ</remarks>
    private const int NotificationOrder = 2;

    /// <summary>通知ダイアログの節に並べるノートの行の上限</summary>
    /// <remarks>超えた分は「ほか n 件」の件数だけを出す</remarks>
    private const int MaxNoteLines = 5;

    /// <summary>押すまで見ていない知らせ</summary>
    /// <remarks>
    /// キーはノートの番号なので、同じノートの知らせは新しい内容の 1 件になる。
    /// 持つ数はノートの数までで、増え続けない。
    /// UI スレッドだけで触る。
    /// </remarks>
    private readonly Dictionary<int, NoteNotice> _unread = [];

    /// <inheritdoc />
    /// <remarks>
    /// UI スレッドから呼ぶ。
    /// 通知を出すときに戻る先として、呼んだスレッドのディスパッチャーを使うため。
    /// </remarks>
    public Task StartAsync()
    {
        var dispatcher = DispatcherQueue.GetForCurrentThread();
        monitor.Start(
            () => features.IsEnabled(NotesFeature.Key),
            found => dispatcher.TryEnqueue(() => Show(found, dispatcher)));
        return Task.CompletedTask;
    }

    /// <summary>通知ダイアログに見ていない知らせを全部並べた節を出す</summary>
    /// <param name="found">新しく見つけた他の人の作成・更新</param>
    /// <param name="dispatcher">UI スレッドのディスパッチャー</param>
    /// <remarks>
    /// 同じノートが見ていない知らせにあるときは新しい内容に入れ替える
    /// 作成の知らせがあったノートはそのあと更新されても作成のままにする
    /// 読む人にとってはまだ見ていない新しいノートのため
    /// </remarks>
    private void Show(IReadOnlyList<NoteNotice> found, DispatcherQueue dispatcher)
    {
        foreach (var notice in found)
        {
            var isCreated = notice.IsCreated || (_unread.TryGetValue(notice.Change.Id, out var old) && old.IsCreated);
            _unread[notice.Change.Id] = notice with { IsCreated = isCreated };
        }

        notifications.Show(new NotificationSection(
            NotificationSourceKey,
            NotificationOrder,
            "ノートの更新",
            BuildItems(),
            OnClicked: () =>
            {
                // 知らせを捨てる前に、開く先を決める。通知ウィンドウが閉じている最中に別のウィンドウを操作しないよう、閉じ終わってから開く
                var newest = _unread.Values.MaxBy(notice => notice.Change.UpdatedAt)!;
                dispatcher.TryEnqueue(DispatcherQueuePriority.Low, () => OpenNotesPage(newest));
            },
            OnClosed: _unread.Clear));
    }

    /// <summary>見ていない知らせから節の項目を作る</summary>
    /// <returns>更新した人ごとの項目と上限を超えた分の件数の項目</returns>
    /// <remarks>
    /// 人は新しい知らせがある順に並べる。
    /// 人の項目の補足に、その人のノートを新しい順に、1 行ずつ並べる。
    /// </remarks>
    private List<NotificationItem> BuildItems()
    {
        var ordered = _unread.Values.OrderByDescending(notice => notice.Change.UpdatedAt).ToList();
        var items = ordered
            .Take(MaxNoteLines)
            .GroupBy(notice => notice.Change.EditorUserId)
            .Select(editor => new NotificationItem(
                NoteNotificationText.Heading(editor.First().EditorName),
                Detail: string.Join('\n', editor.Select(notice => NoteNotificationText.Body(notice.Change.Title, notice.IsCreated)))))
            .ToList();

        if (ordered.Count > MaxNoteLines)
        {
            items.Add(new NotificationItem($"ほか {ordered.Count - MaxNoteLines} 件"));
        }
        return items;
    }

    /// <summary>ノートのページを開いて表示する日をそのノートが有効な日にする</summary>
    /// <param name="notice">開く先のノートの知らせ</param>
    private void OpenNotesPage(NoteNotice notice)
    {
        if (navigation.Open<NotesPage>())
        {
            pages.GetCreatedPage<NotesPage>()?.ViewModel.ShowDay(notice.Change.DayToOpen(notes.Today));
        }
    }
}
