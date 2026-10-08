using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using MmmSdk.Core.Components.Storage;
using MmmSdk.WinUI.Components.Dialogs;
using MmmSdk.WinUI.Components.Errors;
using MmmTool.Reminders.Core;

namespace MmmTool.Reminders.ChannelList;

/// <summary>
/// 送信先の一覧画面。自分が登録した送信先を並べ、追加・編集・削除を行う。
/// </summary>
/// <remarks>
/// 一覧は、開いたときと、追加・編集・削除のあとに、DB から読み直す (件数が少ないので、全部を読む)。
/// 読み込み・保存の失敗 (接続できない・表が無い)は、<see cref="Error"/> に出す。
/// </remarks>
/// <param name="channels">送信先の保存先</param>
/// <param name="dialogs">確認ダイアログを開く</param>
/// <param name="reminderDialogs">登録画面を開く</param>
public sealed partial class NotificationChannelListViewModel(
    INotificationChannelRepository channels, IDialogService dialogs, IReminderDialogService reminderDialogs)
{
    /// <summary>一覧の行</summary>
    public ObservableCollection<NotificationChannelItem> Items { get; } = [];

    /// <summary>読み込み・保存のエラー</summary>
    public ErrorState Error { get; } = new();

    /// <summary>一覧を読み込む</summary>
    /// <returns>読み込みの完了を表すタスク</returns>
    public async Task InitializeAsync()
    {
        Error.Clear();
        try
        {
            var loaded = await channels.GetChannelsAsync(includeDeleted: false);
            Items.Clear();
            foreach (var channel in loaded)
            {
                Items.Add(new NotificationChannelItem(channel));
            }
        }
        catch (DataFileException ex)
        {
            Error.Show(ex.Message);
        }
    }

    /// <summary>新規追加 (登録画面を開く)</summary>
    /// <returns>追加の完了を表すタスク</returns>
    [RelayCommand]
    private async Task AddAsync()
    {
        if (await reminderDialogs.ShowChannelEditAsync(null) is not null)
        {
            await InitializeAsync();
        }
    }

    /// <summary>編集 (登録画面を開く)</summary>
    /// <param name="item">編集する行</param>
    /// <returns>編集の完了を表すタスク</returns>
    [RelayCommand]
    private async Task EditAsync(NotificationChannelItem item)
    {
        if (await reminderDialogs.ShowChannelEditAsync(item.Source) is not null)
        {
            await InitializeAsync();
        }
    }

    /// <summary>削除 (論理削除)</summary>
    /// <param name="item">削除する行</param>
    /// <returns>削除の完了を表すタスク</returns>
    /// <remarks>この送信先を選んでいるリマインダーは送信されなくなるので、確認してから。</remarks>
    [RelayCommand]
    private async Task DeleteAsync(NotificationChannelItem item)
    {
        if (!await dialogs.ConfirmAsync("送信先の削除", $"「{item.Name}」を削除します。\nこの送信先を選んでいるリマインダーは、送信されなくなります。", "削除", "キャンセル"))
        {
            return;
        }

        try
        {
            await channels.SetDeletedAsync(item.Source.Id, isDeleted: true);
        }
        catch (DataFileException ex)
        {
            Error.Show(ex.Message);
            return;
        }
        await InitializeAsync();
    }
}
