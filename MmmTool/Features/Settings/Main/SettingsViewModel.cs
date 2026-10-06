using CommunityToolkit.Mvvm.ComponentModel;
using MmmSdk.Core.Components.Settings;
using MmmSdk.Core.Components.Storage;
using MmmSdk.WinUI.Components.Errors;
using MmmTool.Shell;

namespace MmmTool.Features.Settings.Main;

/// <summary>設定ページの ViewModel</summary>
/// <remarks>各機能の設定の値と画面の状態は、機能ごとの部品 (<see cref="MmmSdk.WinUI.Components.Pages.SettingsSection"/>)が持つ。ここは、ページ全体のこと (タイトル・設定ファイルを読めなかったときの知らせ・機能のオン・オフの一覧)だけ。</remarks>
public sealed class SettingsViewModel : ObservableObject
{
    /// <summary>機能のオン・オフ</summary>
    private readonly FeatureService _features;

    /// <summary>ページのタイトル</summary>
    public string Title => "設定";

    /// <summary>画面に出すエラー (設定ファイルを読めなかったとき・オン・オフを保存できなかったとき)</summary>
    public ErrorState Error { get; } = new();

    /// <summary>オン・オフを切り替えられる機能の一覧 (登録順)</summary>
    public IReadOnlyList<FeatureItem> Features { get; }

    /// <summary>オン・オフを切り替えられるか (設定ファイルを読めたとき)</summary>
    public bool IsEditable { get; }

    /// <summary>ページを作る</summary>
    /// <param name="settings">汎用設定ストア</param>
    /// <param name="features">機能のオン・オフ</param>
    /// <remarks>設定ファイルを読めなかったときは、各部品が上書きして消さないよう、変更を止める (部品は、そのサービスの <c>IsReadOnly</c> で入力欄を無効にする。機能のスイッチも同じ)。</remarks>
    public SettingsViewModel(ISettingsStore settings, FeatureService features)
    {
        _features = features;
        Features = [.. features.Features.Select(f => new FeatureItem(f, features.IsEnabled(f.Key)))];
        IsEditable = !settings.IsReadOnly;

        if (settings.LoadError is { } loadError)
        {
            Error.Show($"{loadError}\n設定を読み込めなかったため、変更を保存できません。");
        }
    }

    /// <summary>スイッチが切り替わったときの処理 (機能のオン・オフを反映する)</summary>
    /// <param name="item">切り替わった行</param>
    /// <returns>反映の完了を表すタスク</returns>
    /// <remarks>
    /// 画面の状態と実際の状態が同じなら何もしない (初めの表示・下の巻き戻しでも、スイッチの切り替えが通知されるため)。
    /// 取りやめた・保存できなかった (読み込めない・ロックや権限での失敗。画面に出す)ときは、スイッチを実際の状態へ戻す。
    /// </remarks>
    public async Task ToggleFeatureAsync(FeatureItem item)
    {
        if (item.IsOn == _features.IsEnabled(item.Key))
        {
            return;
        }

        try
        {
            var result = await _features.SetEnabledAsync(item.Key, item.IsOn);
            if (result == FeatureChangeResult.NotSaved)
            {
                Error.Show("切り替えられませんでした。設定を読み込めなかったため、保存できません。");
            }
        }
        catch (DataFileException ex)
        {
            Error.Show($"切り替えられませんでした。{ex.Message}");
        }

        item.IsOn = _features.IsEnabled(item.Key);
    }
}
