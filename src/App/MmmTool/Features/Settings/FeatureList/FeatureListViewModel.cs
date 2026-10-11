using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using MmmSdk.Core.Components.Features;
using MmmSdk.Core.Components.Settings;
using MmmSdk.Core.Components.Storage;
using MmmSdk.Core.Utilities;
using MmmSdk.WinUI.Components.Errors;

namespace MmmTool.Features.Settings.FeatureList;

/// <summary>設定ページの「機能」の一覧 (機能のオン・オフ・並び順)の ViewModel</summary>
public sealed class FeatureListViewModel : ObservableObject
{
    /// <summary>機能のオン・オフ</summary>
    private readonly FeatureService _features;

    /// <summary>並べ替えの保存を、操作が止まってから 1 回だけ行うための待ち</summary>
    private readonly Debouncer _saveDebouncer = new(TimeSpan.FromMilliseconds(300));

    /// <summary>一覧の並びを、実際の並びに合わせている最中か (このときの変更は、保存しない)</summary>
    private bool _isSyncing;

    /// <summary>画面に出すエラー (オン・オフ・並び順を保存できなかったとき)</summary>
    public ErrorState Error { get; } = new();

    /// <summary>機能の一覧 (利用者が決めた並び順。ドラッグで並べ替える)</summary>
    public ObservableCollection<FeatureItem> Features { get; }

    /// <summary>オン・オフ・並べ替えができるか (設定ファイルを読めたとき)</summary>
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
        Features.CollectionChanged += OnFeaturesChanged;
    }

    /// <summary>一覧の並びが変わったとき (ドラッグでの並べ替え)の処理</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">変更の内容</param>
    /// <remarks>
    /// 並べ替えの終わりの通知 (<c>DragItemsCompleted</c>)は届かなかったので、一覧の変更そのものを見る。
    /// ドラッグは削除と挿入の 2 回の変更になるため、操作が止まってから 1 回だけ保存する。
    /// </remarks>
    private void OnFeaturesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_isSyncing)
        {
            return;
        }

        _saveDebouncer.RunAsync(() => true, _ => SaveOrderAsync().Forget()).Forget();
    }

    /// <summary>ドラッグで並べ替えたあとの処理 (今の並びを保存して反映する)</summary>
    /// <returns>保存の完了を表すタスク</returns>
    /// <remarks>保存できなかったときは、エラーを出して、実際の並びへ戻す。</remarks>
    public async Task SaveOrderAsync()
    {
        var keys = Features.Select(f => f.Key).ToList();
        if (keys.SequenceEqual(_features.Features.Select(f => f.Key)))
        {
            return;
        }

        try
        {
            var saved = await _features.SetOrderAsync(keys);
            if (!saved)
            {
                Error.Show("並べ替えられませんでした。設定を読み込めなかったため、保存できません。");
                SyncOrder();
            }
        }
        catch (DataFileException ex)
        {
            Error.Show($"並べ替えられませんでした。{ex.Message}");
            SyncOrder();
        }
    }

    /// <summary>並び順を、登録の順に戻す</summary>
    /// <returns>戻す処理の完了を表すタスク</returns>
    public async Task ResetOrderAsync()
    {
        try
        {
            if (!await _features.ResetOrderAsync())
            {
                Error.Show("並び順を戻せませんでした。設定を読み込めなかったため、保存できません。");
            }
        }
        catch (DataFileException ex)
        {
            Error.Show($"並び順を戻せませんでした。{ex.Message}");
        }

        SyncOrder();
    }

    /// <summary>一覧の並びを、実際の並び (<see cref="FeatureService.Features"/>)に合わせる</summary>
    private void SyncOrder()
    {
        _isSyncing = true;
        var desired = _features.Features.Select(f => f.Key).ToList();
        for (var i = 0; i < desired.Count; i++)
        {
            var current = Features.ToList().FindIndex(f => f.Key == desired[i]);
            if (current >= 0 && current != i)
            {
                Features.Move(current, i);
            }
        }

        _isSyncing = false;
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
