using System.Collections.ObjectModel;
using MmmSdk.Core.Components.Storage;
using MmmSdk.WinUI.Components.Errors;
using MmmTool.NotificationChannels.Core;
using MmmTool.Users.Core;

namespace MmmTool.Reminders.Settings;

/// <summary>
/// 設定ページの「通知を受け取る機能」の ViewModel。通知を出す機能を並べ、オン・オフを保存する。
/// </summary>
/// <param name="settings">通知を受け取るオン・オフを持つ機能。登録された分</param>
/// <param name="store">ユーザーごとの設定の保存先</param>
/// <param name="access">機能の利用の控え。管理者が無効にした機能は並べない</param>
/// <remarks>
/// リマインダーの機能は、メッセージやノートの名前を持たない。登録された設定を、並べるだけ。
/// 切り替えた操作の直後に保存する。保存に失敗したときは、エラーを出して、保存済みの値に戻す。
/// </remarks>
public sealed class NotificationFeatureSettingsViewModel(IEnumerable<INotificationFeatureSetting> settings, IAppUserSettingRepository store, FeatureAccessCache access)
{
    /// <summary>DB モード (サーバーモード)か。false のときは、部品を出さない</summary>
    public bool IsAvailable { get; } = store.IsAvailable;

    /// <summary>並べる機能</summary>
    public ObservableCollection<NotificationFeatureItem> Items { get; } = [];

    /// <summary>読み込み・保存のエラー</summary>
    public ErrorState Error { get; } = new();

    /// <summary>機能と、保存済みのオン・オフを読む</summary>
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
            var loaded = new List<NotificationFeatureItem>();
            foreach (var setting in settings.Where(setting => access.Get(setting.FeatureNo) != FeatureAccess.Disabled))
            {
                var saved = await store.GetAsync(setting.FeatureNo, NotificationFeatureSettings.CategoryNo, NotificationFeatureSettings.CodeNo);
                loaded.Add(new NotificationFeatureItem(setting, NotificationFeatureSettings.IsOn(saved, setting.DefaultOn), SaveAsync));
            }

            Items.Clear();
            foreach (var item in loaded)
            {
                Items.Add(item);
            }
        }
        catch (DataFileException ex)
        {
            Error.Show($"通知を受け取る機能を読み込めませんでした: {ex.Message}");
        }
    }

    /// <summary>オン・オフを保存する</summary>
    /// <param name="item">値が変わった機能</param>
    /// <returns>保存の完了を表すタスク</returns>
    private async Task SaveAsync(NotificationFeatureItem item)
    {
        try
        {
            await store.SetAsync(item.Setting.FeatureNo, NotificationFeatureSettings.CategoryNo, NotificationFeatureSettings.CodeNo, NotificationFeatureSettings.ToValue(item.IsOn));
            Error.Clear();
        }
        catch (DataFileException ex)
        {
            Error.Show($"保存できませんでした: {ex.Message}");
            await InitializeAsync();
        }
    }
}
