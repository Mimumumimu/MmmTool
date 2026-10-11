using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MmmSdk.Core.Components.Storage;
using MmmSdk.Core.Utilities;
using MmmSdk.WinUI.Components.Errors;
using MmmTool.WorkItems.Core;

namespace MmmTool.WorkItems.ProjectPicker;

/// <summary>
/// 表示する案件を選ぶ画面 (フライアウト)。案件マスターを検索して、チェックで選ぶ。
/// </summary>
/// <param name="service">作業リストの読み書き</param>
/// <remarks>
/// チェックを入れる・外すと、その場で保存し (確認ボタンは無い)、画面の案件を出し入れする。外しても、案件とその中身は消えない。
/// 一覧は数十件ずつ読み、終わりまで来たときに続きを読む。検索語は名前の一部で、入力が止まってから検索する。
/// 選んだ案件は、一覧の先頭にまとめる。
/// </remarks>
public sealed partial class ProjectPickerViewModel(WorkItemService service) : ObservableObject
{
    /// <summary>1 回に読む件数</summary>
    private const int PageSize = 50;

    /// <summary>入力が止まってから検索するまでの待ち時間</summary>
    private static readonly TimeSpan SearchDelay = TimeSpan.FromMilliseconds(300);

    /// <summary>今の検索の番号 (古い検索の結果を捨てるため)</summary>
    private int _version;

    /// <summary>続きを読んでいる最中か</summary>
    private bool _isLoading;

    /// <summary>案件の一覧</summary>
    public ObservableCollection<ProjectChoice> Items { get; } = [];

    /// <summary>画面に出すエラー</summary>
    public ErrorState Error { get; } = new();

    /// <summary>検索語 (名前の一部)</summary>
    [ObservableProperty]
    public partial string Query { get; set; } = "";

    /// <summary>完了済みの案件を除くか (完了した案件で一覧が埋まらないよう、既定はオン)</summary>
    [ObservableProperty]
    public partial bool ActiveOnly { get; set; } = true;

    /// <summary>続きがあるか</summary>
    [ObservableProperty]
    public partial bool HasMore { get; private set; }

    /// <summary>一覧が空か (検索の結果が 0 件のときの案内を出す)</summary>
    [ObservableProperty]
    public partial bool IsEmpty { get; private set; } = true;

    /// <summary>一覧を読み直す (先頭から)</summary>
    /// <returns>読み込みの完了を表すタスク</returns>
    public async Task RefreshAsync()
    {
        var version = ++_version;
        try
        {
            var page = await service.SearchProjectsAsync(Query, ActiveOnly, selectedFirst: true, 0, PageSize);
            if (version != _version)
            {
                return;
            }

            Items.Clear();
            foreach (var entry in page)
            {
                Items.Add(new ProjectChoice(entry));
            }
            HasMore = page.Count == PageSize;
            IsEmpty = Items.Count == 0;
            Error.Clear();
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
            var page = await service.SearchProjectsAsync(Query, ActiveOnly, selectedFirst: true, Items.Count, PageSize);
            if (version != _version)
            {
                return;
            }

            foreach (var entry in page)
            {
                Items.Add(new ProjectChoice(entry));
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

    /// <summary>案件を、表示する案件に選ぶ・外す</summary>
    /// <param name="choice">一覧の 1 件</param>
    /// <param name="selected">選ぶか</param>
    /// <returns>保存の完了を表すタスク</returns>
    public async Task SetSelectedAsync(ProjectChoice choice, bool selected)
    {
        try
        {
            await service.SetProjectSelectedAsync(choice.Id, selected);
            choice.IsSelected = selected;
            Error.Clear();
        }
        catch (DataFileException ex)
        {
            choice.IsSelected = !selected;
            Error.Show(ex.Message);
        }
    }

    /// <summary>検索語が変わったら、入力が止まってから検索し直す</summary>
    /// <param name="value">新しい検索語</param>
    partial void OnQueryChanged(string value) => SearchLaterAsync().Forget();

    /// <summary>完了済みを除くかが変わったら、検索し直す</summary>
    /// <param name="value">除くか</param>
    partial void OnActiveOnlyChanged(bool value) => RefreshAsync().Forget();

    /// <summary>入力が止まるのを待ってから検索する</summary>
    /// <returns>検索の完了を表すタスク</returns>
    private async Task SearchLaterAsync()
    {
        var version = ++_version;
        await Task.Delay(SearchDelay);
        if (version == _version)
        {
            await RefreshAsync();
        }
    }
}
