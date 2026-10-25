using System.Collections.ObjectModel;
using System.Globalization;
using MmmSdk.Core.Components.Storage;
using MmmSdk.WinUI.Components.Errors;
using MmmTool.NotificationChannels.Core;
using MmmTool.Users.Core;

namespace MmmTool.Reminders.Settings;

/// <summary>
/// 設定ページの「受け取り先」の ViewModel。受け取り先を指定できる設定を並べ、選んだ送信先を保存する。
/// </summary>
/// <param name="settings">受け取り先を指定できる設定。登録された分</param>
/// <param name="store">ユーザーごとの設定の保存先</param>
/// <param name="channels">送信先の保存先。自分が使用者の送信先を読む</param>
/// <remarks>
/// リマインダーの機能は、メッセージなどの名前を持たない。登録された設定を、並べるだけ。
/// 選んだ操作の直後に保存する。保存に失敗したときは、エラーを出して、保存済みの選択に戻す。
/// </remarks>
public sealed class ReceiveTargetSettingsViewModel(IEnumerable<IReceiveTargetSetting> settings, IAppUserSettingRepository store, INotificationChannelRepository channels)
{
    /// <summary>DB モード (サーバーモード)か。false のときは、部品を出さない</summary>
    public bool IsAvailable { get; } = store.IsAvailable && channels.IsAvailable;

    /// <summary>並べる設定</summary>
    public ObservableCollection<ReceiveTargetItem> Items { get; } = [];

    /// <summary>読み込み・保存のエラー</summary>
    public ErrorState Error { get; } = new();

    /// <summary>設定と、選べる送信先と、保存済みの選択を読む</summary>
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
            var options = new List<ReceiveChannelOption> { ReceiveChannelOption.None };
            options.AddRange((await channels.GetChannelsAsync(includeDeleted: false)).Select(ReceiveChannelOption.From));

            Items.Clear();
            foreach (var setting in settings)
            {
                var saved = await store.GetAsync(setting.FeatureNo, setting.CategoryNo, setting.CodeNo);
                var item = new ReceiveTargetItem(setting, options, SaveAsync) { IsLoading = true };
                item.Selected = options.FirstOrDefault(option => int.TryParse(saved, CultureInfo.InvariantCulture, out var id) && option.ChannelId == id) ?? ReceiveChannelOption.None;
                item.IsLoading = false;
                Items.Add(item);
            }
        }
        catch (DataFileException ex)
        {
            Error.Show($"受け取り先を読み込めませんでした: {ex.Message}");
        }
    }

    /// <summary>選んだ送信先を保存する。指定しないときは、設定を外す</summary>
    /// <param name="item">選択が変わった設定</param>
    /// <returns>保存の完了を表すタスク</returns>
    private async Task SaveAsync(ReceiveTargetItem item)
    {
        var setting = item.Setting;
        try
        {
            if (item.Selected is { ChannelId: > 0 } selected)
            {
                await store.SetAsync(setting.FeatureNo, setting.CategoryNo, setting.CodeNo, selected.ChannelId.ToString(CultureInfo.InvariantCulture));
            }
            else
            {
                await store.ClearAsync(setting.FeatureNo, setting.CategoryNo, setting.CodeNo);
            }
            Error.Clear();
        }
        catch (DataFileException ex)
        {
            Error.Show($"受け取り先を保存できませんでした: {ex.Message}");
            await InitializeAsync();
        }
    }
}
