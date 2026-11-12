using System.Collections.ObjectModel;
using System.Globalization;
using MmmSdk.Core.Components.Storage;
using MmmSdk.WinUI.Components.Errors;
using MmmTool.NotificationChannels.Core;
using MmmTool.Users.Core;

namespace MmmTool.Reminders.Settings;

/// <summary>設定ページの「通知先」の ViewModel</summary>
/// <param name="store">ユーザーごとの設定の保存先</param>
/// <param name="channels">
/// 送信先の保存先
/// 自分が使用者の送信先を読む
/// </param>
/// <remarks>
/// 通知先を 1 つ選び、保存する。
/// 選べるのは Web 版通知の送信先だけ。
/// 通知先はどの機能にも属さない共通の設定で、番号は <see cref="NotificationTarget"/> にある。
/// メッセージやノートの名前は持たない。
/// 選んだ操作の直後に保存する。
/// 保存に失敗したときはエラーを出して、保存済みの選択に戻す。
/// </remarks>
public sealed class NotificationTargetSettingsViewModel(IAppUserSettingRepository store, INotificationChannelRepository channels)
{
    /// <summary>DB モードか</summary>
    /// <remarks>false のときは部品を出さない</remarks>
    public bool IsAvailable { get; } = store.IsAvailable && channels.IsAvailable;

    /// <summary>並べる選択</summary>
    /// <remarks>読み込んだあとは 1 件</remarks>
    public ObservableCollection<NotificationTargetItem> Items { get; } = [];

    /// <summary>読み込み・保存のエラー</summary>
    public ErrorState Error { get; } = new();

    /// <summary>選べる送信先と保存済みの選択を読む</summary>
    /// <returns>読み込みの完了を表すタスク</returns>
    public async Task InitializeAsync()
    {
        if (!IsAvailable)
        {
            return;
        }

        Error.Clear();
        try
        {
            var options = new List<NotificationChannelOption> { NotificationChannelOption.None };
            // 通知先に選べるのは、Web 版通知の送信先だけ。ntfy と Discord は選べない (PC には通知ダイアログがあり、通知先はスマホの通知のためだけにある)
            options.AddRange((await channels.GetChannelsAsync(includeDeleted: false))
                .Where(channel => channel.Kind == NotificationChannelKind.WebPush)
                .Select(NotificationChannelOption.From));

            var saved = await store.GetAsync(NotificationTarget.FeatureNo, NotificationTarget.CategoryNo, NotificationTarget.CodeNo);
            var item = new NotificationTargetItem(options, SaveAsync) { IsLoading = true };
            item.Selected = options.FirstOrDefault(option => int.TryParse(saved, CultureInfo.InvariantCulture, out var id) && option.ChannelId == id) ?? NotificationChannelOption.None;
            item.IsLoading = false;

            Items.Clear();
            Items.Add(item);
        }
        catch (DataFileException ex)
        {
            Error.Show($"通知先を読み込めませんでした: {ex.Message}");
        }
    }

    /// <summary>選んだ送信先を保存する</summary>
    /// <param name="item">選択が変わった項目</param>
    /// <returns>保存の完了を表すタスク</returns>
    /// <remarks>指定しないときは設定を外す</remarks>
    private async Task SaveAsync(NotificationTargetItem item)
    {
        try
        {
            if (item.Selected is { ChannelId: > 0 } selected)
            {
                await store.SetAsync(NotificationTarget.FeatureNo, NotificationTarget.CategoryNo, NotificationTarget.CodeNo, selected.ChannelId.ToString(CultureInfo.InvariantCulture));
            }
            else
            {
                await store.ClearAsync(NotificationTarget.FeatureNo, NotificationTarget.CategoryNo, NotificationTarget.CodeNo);
            }
            Error.Clear();
        }
        catch (DataFileException ex)
        {
            Error.Show($"通知先を保存できませんでした: {ex.Message}");
            await InitializeAsync();
        }
    }
}
