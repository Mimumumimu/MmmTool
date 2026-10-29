using CommunityToolkit.Mvvm.ComponentModel;
using MmmTool.NotificationChannels.Core;

namespace MmmTool.Reminders.Settings;

/// <summary>
/// 設定ページの「通知を受け取る機能」に並べる、機能 1 件のオン・オフ。
/// </summary>
/// <param name="setting">登録された設定</param>
/// <param name="isOn">読み込んだ、オン・オフ</param>
/// <param name="changed">オン・オフが変わったときの処理。保存する</param>
public sealed partial class NotificationFeatureItem(INotificationFeatureSetting setting, bool isOn, Func<NotificationFeatureItem, Task> changed) : ObservableObject
{
    /// <summary>画面に出す、機能の名前</summary>
    public string Name { get; } = setting.Name;

    /// <summary>登録された設定</summary>
    public INotificationFeatureSetting Setting { get; } = setting;

    /// <summary>オンか。読み込み中は、保存しない</summary>
    [ObservableProperty]
    public partial bool IsOn { get; set; } = isOn;

    /// <summary>読み込みで値を決めている最中か。true の間は、変わりを保存しない</summary>
    public bool IsLoading { get; set; }

    /// <summary>値が変わったとき、読み込み中でなければ保存する</summary>
    /// <param name="value">新しい値</param>
    partial void OnIsOnChanged(bool value)
    {
        if (!IsLoading)
        {
            _ = changed(this);
        }
    }
}
