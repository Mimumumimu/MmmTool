using CommunityToolkit.Mvvm.ComponentModel;
using MmmSdk.Core.Components.Features;
using MmmSdk.Core.Components.Settings;
using MmmSdk.Core.Components.Storage;
using MmmSdk.WinUI.Components.Errors;

namespace MmmTool.Features.Settings.FeatureList;

/// <summary>設定ページの「機能」の一覧 (機能のオン・オフ)の ViewModel</summary>
public sealed class FeatureListViewModel : ObservableObject
{
    /// <summary>機能のオン・オフ</summary>
    private readonly FeatureService _features;

    /// <summary>画面に出すエラー (オン・オフを保存できなかったとき)</summary>
    public ErrorState Error { get; } = new();

    /// <summary>オン・オフを切り替えられる機能の一覧 (並び順の値の順)</summary>
    public IReadOnlyList<FeatureItem> Features { get; }

    /// <summary>オン・オフを切り替えられるか (設定ファイルを読めたとき)</summary>
    public bool IsEditable { get; }

    /// <summary>一覧を作る</summary>
    /// <param name="settings">汎用設定ストア</param>
    /// <param name="features">機能のオン・オフ</param>
    /// <remarks>設定ファイルを読めなかったときは、上書きして消さないよう、スイッチを無効にする。</remarks>
    public FeatureListViewModel(ISettingsStore settings, FeatureService features)
    {
        _features = features;
        Features = [.. features.Features.Select(f => new FeatureItem(f, features.IsEnabled(f.Key)))];
        IsEditable = !settings.IsReadOnly;
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
