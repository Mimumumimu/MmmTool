using CommunityToolkit.Mvvm.ComponentModel;
using MmmSdk.Core.Components.Features;

namespace MmmTool.Features.Settings.Main;

/// <summary>設定ページの「機能」の一覧の 1 行 (機能のオン・オフのスイッチ)</summary>
/// <param name="info">機能の登録情報</param>
/// <param name="isOn">今オンか</param>
public sealed partial class FeatureItem(FeatureInfo info, bool isOn) : ObservableObject
{
    /// <summary>機能のキー</summary>
    public string Key => info.Key;

    /// <summary>機能の名前</summary>
    public string DisplayName => info.DisplayName;

    /// <summary>オンか (スイッチの状態)</summary>
    [ObservableProperty]
    public partial bool IsOn { get; set; } = isOn;
}
