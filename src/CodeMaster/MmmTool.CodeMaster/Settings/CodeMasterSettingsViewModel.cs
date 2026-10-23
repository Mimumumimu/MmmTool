using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MmmSdk.Core.Components.Features;
using MmmTool.CodeMaster.Core;
using MmmTool.CodeMaster.Edit;

namespace MmmTool.CodeMaster.Settings;

/// <summary>
/// 設定ページの「選択肢」の ViewModel。登録された区分を並べ、押された区分の編集画面を開く。
/// </summary>
/// <param name="editors">機能が登録した、名前を編集できる区分</param>
/// <param name="repository">コードマスタの保存先 (編集できるかの判定に使う)</param>
/// <param name="features">機能のオン・オフ</param>
/// <param name="dialogs">編集画面を開く</param>
/// <remarks>
/// 編集できない保存先 (DB モード)と、出せる区分が無いときは、部品そのものを隠す。特定の機能を知らない。
/// 機能のオン・オフが変わったら、並べ直す。部品が画面に載っている間だけ、<see cref="Attach"/> と <see cref="Detach"/> で購読する。
/// </remarks>
public sealed partial class CodeMasterSettingsViewModel(IEnumerable<CodeCategoryEditor> editors, ICodeMasterRepository repository, FeatureService features, ICodeMasterDialogService dialogs) : ObservableObject
{
    /// <summary>登録された区分</summary>
    private readonly IReadOnlyList<CodeCategoryEditor> _editors = [.. editors];

    /// <summary>今出している区分 (機能がオンのもの)</summary>
    public ObservableCollection<CodeCategoryEditor> Items { get; } = [];

    /// <summary>部品を出すか (編集できる保存先で、出す区分がある)</summary>
    [ObservableProperty]
    public partial bool IsVisible { get; private set; }

    /// <summary>購読を始め、区分の並びを作る</summary>
    public void Attach()
    {
        features.Changed += OnFeatureChanged;
        Refresh();
    }

    /// <summary>購読をやめる</summary>
    public void Detach() => features.Changed -= OnFeatureChanged;

    /// <summary>機能のオン・オフが変わったら、区分を並べ直す</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="featureKey">切り替わった機能のキー</param>
    private void OnFeatureChanged(object? sender, string featureKey) => Refresh();

    /// <summary>機能がオンの区分だけを並べ直す</summary>
    private void Refresh()
    {
        Items.Clear();
        if (repository.CanEdit)
        {
            foreach (var editor in _editors.Where(e => features.IsEnabled(e.FeatureKey)))
            {
                Items.Add(editor);
            }
        }
        IsVisible = Items.Count > 0;
    }

    /// <summary>区分の編集画面を開く</summary>
    /// <param name="editor">編集する区分</param>
    /// <returns>編集画面が閉じるまでの完了を表すタスク</returns>
    [RelayCommand]
    private Task OpenAsync(CodeCategoryEditor editor) => dialogs.ShowEditAsync(editor);
}
