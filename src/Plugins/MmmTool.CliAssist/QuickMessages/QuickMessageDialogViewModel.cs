using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MmmSdk.Core.Components.Storage;
using MmmSdk.WinUI.Components.Errors;
using MmmTool.CliAssist.Core;

namespace MmmTool.CliAssist.QuickMessages;

/// <summary>よく使う文の編集ダイアログの ViewModel</summary>
public sealed partial class QuickMessageDialogViewModel : ObservableObject
{
    /// <summary>よく使う文</summary>
    private readonly CliQuickMessageService _quickMessages;

    /// <summary>ViewModel を作る</summary>
    /// <param name="quickMessages">よく使う文</param>
    public QuickMessageDialogViewModel(CliQuickMessageService quickMessages)
    {
        _quickMessages = quickMessages;
        RootItems.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasNoItems));
    }

    /// <summary>グループが別のグループの中へ入った (ドロップで)</summary>
    /// <remarks>ドロップの処理の途中で呼ばれる。画面は、処理が終わってから <see cref="MoveNestedGroupsOut"/> を呼ぶ。</remarks>
    public event EventHandler? GroupNested;

    /// <summary>追加した項目の入力欄へフォーカスを移してほしい</summary>
    public event EventHandler? EditRequested;

    /// <summary>編集中の一覧 (保存するまで、サービスには反映しない)</summary>
    /// <remarks>文とグループが並び、グループは中に文を持つ。</remarks>
    public ObservableCollection<QuickMessageEditItem> RootItems { get; } = [];

    /// <summary>一覧で選ばれている項目</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection), nameof(IsGroupSelected), nameof(IsMessageSelected))]
    [NotifyCanExecuteChangedFor(nameof(RemoveCommand))]
    public partial QuickMessageEditItem? SelectedItem { get; set; }

    /// <summary>項目が選ばれているか</summary>
    public bool HasSelection => SelectedItem is not null;

    /// <summary>グループが選ばれているか (右にグループ名の入力欄を出す条件)</summary>
    public bool IsGroupSelected => SelectedItem is { IsGroup: true };

    /// <summary>文が選ばれているか (右に表示名と文の入力欄を出す条件)</summary>
    public bool IsMessageSelected => SelectedItem is { IsGroup: false };

    /// <summary>一覧が空か</summary>
    public bool HasNoItems => RootItems.Count == 0;

    /// <summary>エラー</summary>
    public ErrorState Error { get; } = new();

    /// <summary>保存済みの内容で、編集中の一覧を作り直す</summary>
    public void Initialize()
    {
        RootItems.Clear();
        foreach (var message in _quickMessages.Messages)
        {
            Watch(QuickMessageEditItem.From(message));
        }
        SelectedItem = RootItems.FirstOrDefault();
        Error.Clear();
    }

    /// <summary>文を追加して選ぶ</summary>
    /// <remarks>
    /// 選びがグループなら、その中の末尾に入れる。
    /// 文なら、同じ場所の直後に入れる。未選択なら、一覧の末尾に入れる。
    /// </remarks>
    [RelayCommand]
    private void AddMessage()
    {
        var item = QuickMessageEditItem.CreateMessage();
        if (SelectedItem is { IsGroup: true } group)
        {
            group.Children!.Add(item);
        }
        else if (SelectedItem is { } selected && FindParentCollection(selected) is { } siblings)
        {
            siblings.Insert(siblings.IndexOf(selected) + 1, item);
        }
        else
        {
            RootItems.Add(item);
        }
        Select(item);
    }

    /// <summary>グループを追加して選ぶ</summary>
    /// <remarks>グループは一覧の直下だけに置く。選びがグループ、または、グループの中の文なら、そのグループの直後に入れる。未選択なら、一覧の末尾に入れる。</remarks>
    [RelayCommand]
    private void AddGroup()
    {
        var previous = SelectedItem is { IsGroup: true } selected ? selected : FindGroupOf(SelectedItem);
        var item = Watch(QuickMessageEditItem.CreateGroup(), insertAfter: previous);
        Select(item);
    }

    /// <summary>選ばれている項目を削除する (隣の項目を選ぶ)</summary>
    /// <remarks>グループは、中の文もまとめて削除する。確認は出さない。保存するまでは、「キャンセル」で元に戻せるため。</remarks>
    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void Remove()
    {
        if (SelectedItem is not { } item || FindParentCollection(item) is not { } siblings)
        {
            return;
        }

        var owner = FindGroupOf(item);
        var index = siblings.IndexOf(item);
        siblings.Remove(item);
        SelectedItem = siblings.Count > 0
            ? siblings[Math.Min(index, siblings.Count - 1)]
            : owner ?? RootItems.FirstOrDefault();
    }

    /// <summary>別のグループの中にあるグループを、一覧の直下へ出す</summary>
    /// <remarks>
    /// グループの中にグループは入れないので、ドロップで入ったときは、入った先のグループの直後へ出す。
    /// ドロップの処理が終わってから呼ぶ。処理の途中で一覧を変えると、一覧が壊れるため。
    /// </remarks>
    public void MoveNestedGroupsOut()
    {
        foreach (var owner in RootItems.Where(item => item.IsGroup).ToList())
        {
            foreach (var nested in owner.Children!.Where(child => child.IsGroup).ToList())
            {
                owner.Children!.Remove(nested);
                RootItems.Insert(RootItems.IndexOf(owner) + 1, nested);
                Select(nested, requestEdit: false);
            }
        }
    }

    /// <summary>入力を調べて保存する</summary>
    /// <returns>保存できたら true で、ダイアログを閉じてよい。名前や文が空の項目がある・保存に失敗したときは false</returns>
    public async Task<bool> SaveAsync()
    {
        if (FindInvalid() is { } found)
        {
            Select(found.Item);
            Error.Show(found.Message);
            return false;
        }

        try
        {
            await _quickMessages.SaveAsync(RootItems.Select(item => item.ToMessage()));
        }
        catch (DataFileException ex)
        {
            Error.Show(ex.Message);
            return false;
        }
        return true;
    }

    /// <summary>名前が空のグループ・文が空の文を、表示の順に探す</summary>
    /// <returns>最初に見つけた項目とエラーの文。無ければ null</returns>
    private (QuickMessageEditItem Item, string Message)? FindInvalid()
    {
        foreach (var item in EnumerateInOrder())
        {
            if (item.IsGroup && string.IsNullOrWhiteSpace(item.Label))
            {
                return (item, "グループ名が空の項目があります。入力するか、削除してください");
            }
            if (!item.IsGroup && string.IsNullOrWhiteSpace(item.Text))
            {
                return (item, "文が空の項目があります。入力するか、削除してください");
            }
        }
        return null;
    }

    /// <summary>画面に出る順に、グループとその中の文を並べる</summary>
    /// <returns>グループの直後に、その中の文が続く並び</returns>
    private IEnumerable<QuickMessageEditItem> EnumerateInOrder()
    {
        foreach (var item in RootItems)
        {
            yield return item;
            foreach (var child in item.Children ?? [])
            {
                yield return child;
            }
        }
    }

    /// <summary>グループを一覧に入れ、グループの中にグループが入らないよう見張る</summary>
    /// <param name="group">入れる項目 (グループでなければ、そのまま末尾に入れる)</param>
    /// <param name="insertAfter">この項目の直後に入れる。null なら末尾</param>
    /// <returns>入れた項目</returns>
    private QuickMessageEditItem Watch(QuickMessageEditItem group, QuickMessageEditItem? insertAfter = null)
    {
        if (group.Children is { } children)
        {
            children.CollectionChanged += OnGroupChildrenChanged;
        }

        if (insertAfter is not null)
        {
            RootItems.Insert(RootItems.IndexOf(insertAfter) + 1, group);
        }
        else
        {
            RootItems.Add(group);
        }
        return group;
    }

    /// <summary>グループの中身が変わったとき、グループが入ったかを調べる</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">変更の内容</param>
    private void OnGroupChildrenChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action is NotifyCollectionChangedAction.Add && e.NewItems?.OfType<QuickMessageEditItem>().Any(item => item.IsGroup) == true)
        {
            GroupNested?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>項目を選び、入力欄へのフォーカスを求める</summary>
    /// <param name="item">選ぶ項目</param>
    /// <param name="requestEdit">入力欄へのフォーカスを求めるか</param>
    /// <remarks>閉じたグループの中の項目は、グループを開いてから選ぶ。</remarks>
    private void Select(QuickMessageEditItem item, bool requestEdit = true)
    {
        if (FindGroupOf(item) is { } group)
        {
            group.IsExpanded = true;
        }
        SelectedItem = item;
        if (requestEdit)
        {
            EditRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>項目が入っているコレクション (一覧の直下または、グループの中)を探す</summary>
    /// <param name="item">探す項目</param>
    /// <returns>項目が入っているコレクション。見つからなければ null</returns>
    /// <remarks>ドロップで動かしたあとも、その時点の構造から探す。</remarks>
    private ObservableCollection<QuickMessageEditItem>? FindParentCollection(QuickMessageEditItem item)
        => RootItems.Contains(item) ? RootItems : FindGroupOf(item)?.Children;

    /// <summary>文が入っているグループを探す</summary>
    /// <param name="item">探す項目 (null のこともある)</param>
    /// <returns>項目が中に入っているグループ。一覧の直下にある・グループそのもの・null なら null</returns>
    private QuickMessageEditItem? FindGroupOf(QuickMessageEditItem? item)
        => item is null ? null : RootItems.FirstOrDefault(group => group.Children?.Contains(item) == true);
}
