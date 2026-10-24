using Microsoft.UI.Dispatching;
using MmmSdk.Core.Components.Features;
using MmmSdk.Core.Components.Notifications;
using MmmSdk.Core.Utilities;
using MmmSdk.WinUI.Components.Notifications;
using MmmSdk.WinUI.Components.Pages;
using MmmTool.Messages.Core;
using MmmTool.Messages.Main;

namespace MmmTool.Messages;

/// <summary>メッセージの起動時の準備。読み続けるループを始め、新着を通知ダイアログで知らせる</summary>
/// <param name="sync">会話とメッセージを読み続ける部品</param>
/// <param name="notifications">通知ダイアログの表示</param>
/// <param name="navigation">サイドバーのページを開く口</param>
/// <param name="pages">作ったページ。会話を開くため</param>
/// <param name="features">機能のオン・オフ</param>
/// <remarks>
/// 読み続ける部品は、スレッドプールのスレッドから知らせてくるので、UI スレッドに切り替えて、通知ダイアログに節を出す。
/// 出すのは、他の人から届いた新着で、いま前面で見ている会話以外のもの。起動してから届いた分だけで、閉じている間に届いた分は出さない。
/// 受け取り先が ntfy や Discord の人も、PC のダイアログが出る。ダイアログは PC のアプリだけのもので、受け取り先とは関係しない。
/// 節をクリックしたら、メッセージのページで、その会話を開く。
/// </remarks>
public sealed class MessagesStartup(
    MessageSyncService sync,
    INotificationDialogService notifications,
    INavigationService navigation,
    IPageCache pages,
    IFeatureStatus features) : IStartupTask
{
    /// <summary>通知ダイアログの節の、送り元のキー</summary>
    private const string NotificationSourceKey = "Messages";

    /// <summary>通知ダイアログの節の並びの値。サイドバーの並び (メッセージ 5)と同じ</summary>
    private const int NotificationOrder = 5;

    /// <inheritdoc />
    /// <remarks>UI スレッドから呼ぶ。通知を出すときに戻る先として、呼んだスレッドのディスパッチャーを使うため。</remarks>
    public Task StartAsync()
    {
        var dispatcher = DispatcherQueue.GetForCurrentThread();
        sync.NewMessagesFromOthers += messages => dispatcher.TryEnqueue(() => Show(messages, dispatcher));
        sync.Start(() => features.IsEnabled(MessagesFeature.Key));
        return Task.CompletedTask;
    }

    /// <summary>通知ダイアログに、新着を並べた節を出す</summary>
    /// <param name="messages">他の人から届いた新着</param>
    /// <param name="dispatcher">UI スレッドのディスパッチャー</param>
    private void Show(IReadOnlyList<ChatMessage> messages, DispatcherQueue dispatcher)
    {
        // いま前面で見ている会話の新着は、出さない
        var front = sync.GetFrontConversationId();
        var fresh = messages.Where(message => message.ConversationId != front).ToList();
        if (fresh.Count == 0)
        {
            return;
        }

        var first = fresh[0];
        notifications.Show(new NotificationSection(
            NotificationSourceKey,
            NotificationOrder,
            "メッセージ",
            [.. fresh.Select(message => new NotificationItem(message.SenderName, Detail: FirstLine(message.Body)))],
            OnClicked: () => dispatcher.TryEnqueue(
                // 通知ウィンドウが閉じている最中に別のウィンドウを操作しないよう、閉じ終わってから開く
                DispatcherQueuePriority.Low, () => OpenConversation(first))));
    }

    /// <summary>メッセージのページを開いて、会話を開く</summary>
    /// <param name="message">開く会話の新着</param>
    private void OpenConversation(ChatMessage message)
    {
        if (navigation.Open<MessagesPage>())
        {
            pages.GetCreatedPage<MessagesPage>()?.ViewModel.OpenConversationAsync(message.ConversationId, message.Id).Forget();
        }
    }

    /// <summary>本文の 1 行目を取る</summary>
    /// <param name="text">本文</param>
    /// <returns>最初の改行より前の文字</returns>
    private static string FirstLine(string text)
    {
        var end = text.AsSpan().IndexOfAny('\r', '\n');
        return end < 0 ? text : text[..end];
    }
}
