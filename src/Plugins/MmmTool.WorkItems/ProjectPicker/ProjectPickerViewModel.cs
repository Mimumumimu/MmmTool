using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MmmSdk.Core.Components.Storage;
using MmmSdk.Core.Utilities;
using MmmSdk.WinUI.Components.Errors;
using MmmTool.WorkItems.Core;

namespace MmmTool.WorkItems.ProjectPicker;

/// <summary>表示する案件を選ぶ画面で、フライアウトで出す</summary>
/// <param name="service">作業管理の読み書き</param>
/// <remarks>
/// 案件マスターを検索して、チェックで選ぶ。
/// チェックを入れる・外すとその場で保存する。
/// 確認ボタンは無い。
/// 画面の案件を出し入れする。
/// 外しても案件とその中身は消えない。
/// 一覧は数十件ずつ読み、終わりまで来たときに続きを読む。
/// 検索語は名前の一部で、入力が止まってから検索する。
/// 既定では表示している案件だけを出す。
/// 完了した案件も出るのは外したい案件がそのまま見つかるため。
/// 「非表示の案件も表示」をオンにすると非表示の案件も加わる。
/// 「非表示の案件も表示」がオフのときは外した案件がその場で一覧から消える。
/// 戻すにはオンにして入れ直す。
/// オンのときは外した行が残る。
/// 選んだ案件は一覧の先頭にまとめる。
/// </remarks>
public sealed partial class ProjectPickerViewModel(WorkItemService service) : ObservableObject
{
    /// <summary>1 回に読む件数</summary>
    private const int PageSize = 50;

    /// <summary>入力が止まってから検索するまでの待ち時間</summary>
    private static readonly TimeSpan SearchDelay = TimeSpan.FromMilliseconds(300);

    /// <summary>検索の結果が 0 件のときの案内</summary>
    private const string NoMatchText = "該当する案件がありません";

    /// <summary>表示中の案件が 0 件のときの案内</summary>
    /// <remarks>検索語が空で、「非表示の案件も表示」がオフ。</remarks>
    private const string NoSelectedText = "表示中の案件がありません。「非表示の案件も表示」で、非表示の案件を出せます";

    /// <summary>読み込んだあとに外した案件の数で、外すと +1、入れ直すと -1</summary>
    /// <remarks>続きを読む位置のずれを直すため</remarks>
    private int _selectionShift;

    /// <summary>今の検索の番号</summary>
    /// <remarks>古い検索の結果を捨てるため</remarks>
    private int _version;

    /// <summary>続きを読んでいる最中か</summary>
    private bool _isLoading;

    /// <summary>案件の一覧</summary>
    public ObservableCollection<ProjectChoice> Items { get; } = [];

    /// <summary>画面に出すエラー</summary>
    public ErrorState Error { get; } = new();

    /// <summary>検索語</summary>
    [ObservableProperty]
    public partial string Query { get; set; } = "";

    /// <summary>非表示の案件も一覧に出すか</summary>
    /// <remarks>既定はオフで、オフのときは表示している案件だけを出す。</remarks>
    [ObservableProperty]
    public partial bool ShowHidden { get; set; }

    /// <summary>続きがあるか</summary>
    [ObservableProperty]
    public partial bool HasMore { get; private set; }

    /// <summary>一覧が空か</summary>
    /// <remarks>検索の結果が 0 件のときの案内を出す</remarks>
    [ObservableProperty]
    public partial bool IsEmpty { get; private set; } = true;

    /// <summary>一覧が空のときの案内</summary>
    [ObservableProperty]
    public partial string EmptyText { get; private set; } = NoMatchText;

    /// <summary>フライアウトを開いたときの読み込み</summary>
    /// <returns>読み込みの完了を表すタスク</returns>
    /// <remarks>「非表示の案件も表示」をオフに戻して、先頭から読む。</remarks>
    public Task OpenAsync()
    {
        if (ShowHidden)
        {
            // 変えると、変更の通知で読み直す
            ShowHidden = false;
            return Task.CompletedTask;
        }

        return RefreshAsync();
    }

    /// <summary>一覧を読み直す</summary>
    /// <returns>読み込みの完了を表すタスク</returns>
    public async Task RefreshAsync()
    {
        var version = ++_version;
        try
        {
            var page = await service.SearchProjectsAsync(Query, activeOnly: false, selectedOnly: !ShowHidden, selectedFirst: true, 0, PageSize);
            if (version != _version)
            {
                return;
            }

            Items.Clear();
            _selectionShift = 0;
            foreach (var entry in page)
            {
                Items.Add(new ProjectChoice(entry));
            }
            HasMore = page.Count == PageSize;
            IsEmpty = Items.Count == 0;
            EmptyText = !ShowHidden && Query.Length == 0 ? NoSelectedText : NoMatchText;
            Error.Clear();
        }
        catch (DataFileException ex)
        {
            Error.Show(ex.Message);
        }
    }

    /// <summary>一覧の続きを読む</summary>
    /// <returns>読み込みの完了を表すタスク</returns>
    /// <remarks>終わりまで来たとき</remarks>
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
            // 外した案件は、問い合わせの結果から抜ける。読み始める位置は、その分だけ前にずらす
            var offset = Math.Max(0, Items.Count - Math.Max(0, _selectionShift));
            var page = await service.SearchProjectsAsync(Query, activeOnly: false, selectedOnly: !ShowHidden, selectedFirst: true, offset, PageSize);
            if (version != _version)
            {
                return;
            }

            // ずらした分で、すでに出ている案件を読み直すことがあるので、重ねて足さない
            var loaded = Items.Select(item => item.Id).ToHashSet();
            foreach (var entry in page.Where(entry => !loaded.Contains(entry.Id)))
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

    /// <summary>案件を表示する案件に選ぶ・外す</summary>
    /// <param name="choice">一覧の 1 件</param>
    /// <param name="selected">選ぶか</param>
    /// <returns>保存の完了を表すタスク</returns>
    public async Task SetSelectedAsync(ProjectChoice choice, bool selected)
    {
        try
        {
            await service.SetProjectSelectedAsync(choice.Id, selected);
            choice.IsSelected = selected;
            if (!selected && !ShowHidden)
            {
                // 非表示にした案件は、「非表示の案件も表示」を入れるまで、一覧から消す
                Items.Remove(choice);
                IsEmpty = Items.Count == 0;
                EmptyText = Query.Length == 0 ? NoSelectedText : NoMatchText;
            }
            else
            {
                _selectionShift += selected ? -1 : 1;
            }
            Error.Clear();
        }
        catch (DataFileException ex)
        {
            choice.IsSelected = !selected;
            Error.Show(ex.Message);
        }
    }

    /// <summary>検索語が変わったら入力が止まってから検索し直す</summary>
    /// <param name="value">新しい検索語</param>
    partial void OnQueryChanged(string value) => SearchLaterAsync().Forget();

    /// <summary>非表示の案件も出すかが変わったら検索し直す</summary>
    /// <param name="value">出すか</param>
    partial void OnShowHiddenChanged(bool value) => RefreshAsync().Forget();

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
