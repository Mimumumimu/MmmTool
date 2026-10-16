using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MmmSdk.Core.Components.Storage;
using MmmSdk.Core.Utilities;
using MmmSdk.WinUI.Components.Errors;
using MmmSdk.WinUI.Components.Pages;
using MmmTool.WorkItems.Core;

namespace MmmTool.WorkItems.ProjectPicker;

/// <summary>
/// 表示する案件を選ぶ画面 (フライアウト)。案件マスターを検索して、チェックで選ぶ。
/// </summary>
/// <param name="service">作業管理の読み書き</param>
/// <param name="busyCursor">マウスカーソルを処理待ちの形にする口</param>
/// <remarks>
/// チェックを入れる・外すと、その場で保存し (確認ボタンは無い)、画面の案件を出し入れする。外しても、案件とその中身は消えない。
/// 出し入れの間 (読み込み・表の更新)は、マウスカーソルを砂時計にする。
/// 一覧は数十件ずつ読み、終わりまで来たときに続きを読む。検索語は名前の一部で、入力が止まってから検索する。
/// 既定では、表示している案件だけを出す (完了した案件も出る。外したい案件が、そのまま見つかるため)。「非表示の案件も表示」をオンにすると、非表示の案件も加わる。
/// 「非表示の案件も表示」がオフのときは、外した案件がその場で一覧から消える (戻すには、オンにして入れ直す)。オンのときは、外した行が残る。選んだ案件は、一覧の先頭にまとめる。
/// </remarks>
public sealed partial class ProjectPickerViewModel(WorkItemService service, IBusyCursor busyCursor) : ObservableObject
{
    /// <summary>1 回に読む件数</summary>
    private const int PageSize = 50;

    /// <summary>入力が止まってから検索するまでの待ち時間</summary>
    private static readonly TimeSpan SearchDelay = TimeSpan.FromMilliseconds(300);

    /// <summary>検索の結果が 0 件のときの案内</summary>
    private const string NoMatchText = "該当する案件がありません";

    /// <summary>表示中の案件が 0 件のときの案内 (検索語が空で、「非表示の案件も表示」がオフ)</summary>
    private const string NoSelectedText = "表示中の案件がありません。「非表示の案件も表示」で、非表示の案件を出せます";

    /// <summary>読み込んだあとに外した案件の数 (外すと +1、入れ直すと -1。続きを読む位置のずれを直すため)</summary>
    private int _selectionShift;

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

    /// <summary>非表示の案件も一覧に出すか (既定はオフ。オフのときは、表示している案件だけを出す)</summary>
    [ObservableProperty]
    public partial bool ShowHidden { get; set; }

    /// <summary>続きがあるか</summary>
    [ObservableProperty]
    public partial bool HasMore { get; private set; }

    /// <summary>一覧が空か (検索の結果が 0 件のときの案内を出す)</summary>
    [ObservableProperty]
    public partial bool IsEmpty { get; private set; } = true;

    /// <summary>一覧が空のときの案内</summary>
    [ObservableProperty]
    public partial string EmptyText { get; private set; } = NoMatchText;

    /// <summary>フライアウトを開いたときの読み込み (「非表示の案件も表示」をオフに戻して、先頭から読む)</summary>
    /// <returns>読み込みの完了を表すタスク</returns>
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

    /// <summary>一覧を読み直す (先頭から)</summary>
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

    /// <summary>案件を、表示する案件に選ぶ・外す</summary>
    /// <param name="choice">一覧の 1 件</param>
    /// <param name="selected">選ぶか</param>
    /// <returns>保存の完了を表すタスク</returns>
    public async Task SetSelectedAsync(ProjectChoice choice, bool selected)
    {
        try
        {
            await busyCursor.RunAsync(() => service.SetProjectSelectedAsync(choice.Id, selected));
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

    /// <summary>検索語が変わったら、入力が止まってから検索し直す</summary>
    /// <param name="value">新しい検索語</param>
    partial void OnQueryChanged(string value) => SearchLaterAsync().Forget();

    /// <summary>非表示の案件も出すかが変わったら、検索し直す</summary>
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
