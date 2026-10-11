using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MmmSdk.Core.Components.Storage;
using MmmSdk.Core.Utilities;
using MmmSdk.WinUI.Components.Dialogs;
using MmmSdk.WinUI.Components.Errors;
using MmmTool.WorkItems.Core;

namespace MmmTool.WorkItems.ProjectManager;

/// <summary>
/// 案件の管理の画面。案件マスター (全員で共有する案件の一覧)を一覧で見て、登録・名前の変更・削除をする。
/// </summary>
/// <param name="service">作業管理の読み書き</param>
/// <param name="confirm">確認ダイアログ</param>
/// <remarks>
/// 案件は、「新しい案件」で出る入力中の行に名前を入れて確定すると追加する (空のまま・Esc は保存しない)。追加すると、作った人の表示する案件に加わる。削除は論理削除で、中のグループ・作業も消える (確認に件数を出す)。
/// 更新してよいかは、サービスの更新の入口 (<see cref="IWorkItemEditPolicy"/>)が決める。
/// </remarks>
public sealed partial class ProjectManagerViewModel(WorkItemService service, IDialogService confirm) : ObservableObject
{
    /// <summary>1 回に読む件数</summary>
    private const int PageSize = 50;

    /// <summary>入力中の新しい行 (無ければ null)</summary>
    private ProjectManagerItem? _pending;

    /// <summary>新しい案件の作成の確認を出している最中か</summary>
    private bool _isConfirming;

    /// <summary>今の読み込みの番号 (古い結果を捨てるため)</summary>
    private int _version;

    /// <summary>続きを読んでいる最中か</summary>
    private bool _isLoading;

    /// <summary>案件の一覧</summary>
    public ObservableCollection<ProjectManagerItem> Items { get; } = [];

    /// <summary>画面に出すエラー</summary>
    public ErrorState Error { get; } = new();

    /// <summary>完了済みの案件を除くか (既定はオン)</summary>
    [ObservableProperty]
    public partial bool ActiveOnly { get; set; } = true;

    /// <summary>入力中の行に、入力のフォーカスを移してほしいときに起こす (画面が受ける)</summary>
    public event Action<ProjectManagerItem>? PendingFocusRequested;

    /// <summary>続きがあるか</summary>
    [ObservableProperty]
    public partial bool HasMore { get; private set; }

    /// <summary>一覧が空か</summary>
    [ObservableProperty]
    public partial bool IsEmpty { get; private set; } = true;

    /// <summary>一覧を読み直す (先頭から)</summary>
    /// <returns>読み込みの完了を表すタスク</returns>
    public async Task RefreshAsync()
    {
        var version = ++_version;
        try
        {
            var page = await service.SearchProjectsAsync("", ActiveOnly, selectedFirst: false, 0, PageSize);
            if (version != _version)
            {
                return;
            }

            _pending = null;
            Items.Clear();
            foreach (var entry in page)
            {
                Items.Add(new ProjectManagerItem(entry));
            }
            HasMore = page.Count == PageSize;
            IsEmpty = Items.Count == 0;
        }
        catch (DataFileException ex)
        {
            Error.Show(ex.Message);
        }
    }

    /// <summary>一覧の続きを読む (終わりまで来たとき)</summary>
    /// <returns>読み込みの完了を表すタスク</returns>
    public async Task LoadMoreAsync()
    {
        if (!HasMore || _isLoading)
        {
            return;
        }

        _isLoading = true;
        var version = _version;
        try
        {
            var page = await service.SearchProjectsAsync("", ActiveOnly, selectedFirst: false, Items.Count, PageSize);
            if (version != _version)
            {
                return;
            }

            foreach (var entry in page)
            {
                Items.Add(new ProjectManagerItem(entry));
            }
            HasMore = page.Count == PageSize;
        }
        catch (DataFileException ex)
        {
            Error.Show(ex.Message);
        }
        finally
        {
            _isLoading = false;
        }
    }

    /// <summary>案件の名前の入力を、確定する</summary>
    /// <param name="item">一覧の 1 件</param>
    /// <returns>保存の完了を表すタスク</returns>
    /// <remarks>入力が受け入れられなかったときは、理由を出して、保存されている名前に戻す。</remarks>
    public async Task RenameAsync(ProjectManagerItem item)
    {
        var name = item.Name.Trim();
        item.IsEditing = false;
        if (name == item.SavedName)
        {
            item.Name = item.SavedName;
            return;
        }

        try
        {
            var error = await service.UpdateProjectAsync(item.Id, name, null);
            if (error is null)
            {
                item.SavedName = name;
                item.Name = name;
                Error.Clear();
            }
            else
            {
                item.Name = item.SavedName;
                Error.Show(error);
            }
        }
        catch (DataFileException ex)
        {
            item.Name = item.SavedName;
            Error.Show(ex.Message);
        }
    }

    /// <summary>確認してから、案件を削除する (論理削除。中のグループ・作業も)</summary>
    /// <param name="item">一覧の 1 件</param>
    /// <returns>削除の完了を表すタスク</returns>
    public async Task DeleteAsync(ProjectManagerItem item)
    {
        if (item.IsNew)
        {
            CancelNew(item);
            return;
        }

        try
        {
            var impact = await service.GetDeleteImpactAsync(new WorkKey(WorkRowKind.Project, item.Id));
            var message = $"案件「{item.SavedName}」を、中身ごと削除します。\nグループ {impact.Groups} 件・作業 {impact.Works} 件・日ごとの記録 {impact.Records} 件が、削除されます。";
            if (!await confirm.ConfirmAsync("削除の確認", message, "削除", "キャンセル"))
            {
                return;
            }

            // 案件は全員で共有するので、もう一度確かめる
            if (!await confirm.ConfirmAsync("削除の最終確認", $"本当に、案件「{item.SavedName}」を削除しますか？\n全員の作業管理から、この案件が消えます。", "削除", "キャンセル"))
            {
                return;
            }

            var error = await service.DeleteProjectAsync(item.Id);
            if (error is not null)
            {
                Error.Show(error);
                return;
            }

            Items.Remove(item);
            IsEmpty = Items.Count == 0;
            Error.Clear();
        }
        catch (DataFileException ex)
        {
            Error.Show(ex.Message);
        }
    }

    /// <summary>入力中の行を、先頭に出す (すでにあるときは、それを使う)</summary>
    /// <returns>入力中の行</returns>
    /// <remarks>名前が入って確定するまで、保存しない。</remarks>
    [RelayCommand]
    private void BeginAdd()
    {
        if (_pending is not null)
        {
            PendingFocusRequested?.Invoke(_pending);
            return;
        }

        _pending = ProjectManagerItem.CreateNew();
        Items.Insert(0, _pending);
        IsEmpty = false;
        PendingFocusRequested?.Invoke(_pending);
    }

    /// <summary>入力中の行を、確認してから作成する (名前が空なら、保存せずに消す)</summary>
    /// <param name="item">入力中の行</param>
    /// <returns>作成の完了を表すタスク</returns>
    /// <remarks>
    /// 案件は全員で共有するので、Enter を押したときだけ、確認してから保存する。
    /// フォーカスが外れただけでは保存しない (行は残り、入力を続けられる)。確認で取りやめたときも、行を残す。
    /// </remarks>
    public async Task CreateAsync(ProjectManagerItem item)
    {
        if (item != _pending || _isConfirming)
        {
            return;
        }

        var name = item.Name.Trim();
        if (name.Length == 0)
        {
            CancelNew(item);
            return;
        }

        _isConfirming = true;
        try
        {
            if (!await confirm.ConfirmAsync("案件の作成", $"案件「{name}」を作成します。\n案件は、全員で共有する一覧に加わります。", "作成", "キャンセル"))
            {
                PendingFocusRequested?.Invoke(item);
                return;
            }

            var result = await service.AddProjectAsync(name);
            if (result.Error is { } error)
            {
                // 入力を残して、直せるようにする
                Error.Show(error);
                return;
            }

            _pending = null;
            Error.Clear();
            await RefreshAsync();
        }
        catch (DataFileException ex)
        {
            Error.Show(ex.Message);
        }
        finally
        {
            _isConfirming = false;
        }
    }

    /// <summary>閉じてよいかを確かめる (名前を入力中の新しい案件があるときだけ、確認する)</summary>
    /// <returns>閉じてよければ true</returns>
    public async Task<bool> ConfirmCloseAsync()
    {
        if (_pending is not { } pending || pending.Name.Trim().Length == 0)
        {
            return true;
        }

        return await confirm.ConfirmAsync("案件の作成を中止", $"案件「{pending.Name.Trim()}」は、まだ作成していません。\n作成せずに閉じますか？", "閉じる", "戻る");
    }

    /// <summary>入力中の行を、保存せずに消す</summary>
    /// <param name="item">入力中の行</param>
    public void CancelNew(ProjectManagerItem item)
    {
        if (item != _pending)
        {
            return;
        }

        _pending = null;
        Items.Remove(item);
        IsEmpty = Items.Count == 0;
    }

    /// <summary>完了済みを除くかが変わったら、読み直す</summary>
    /// <param name="value">除くか</param>
    partial void OnActiveOnlyChanged(bool value) => RefreshAsync().Forget();
}
