using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MmmTool.Core.Entities;
using MmmTool.Core.Repositories;
using MmmTool.Core.Services;
using MmmTool.Services;

namespace MmmTool.ViewModels;

/// <summary>
/// リンク編集ページ。リンクメニューのツリーを編集して保存する。
/// </summary>
public sealed partial class LinkEditorViewModel : ObservableObject
{
    /// <summary>パスの入力が止まってから種類を調べるまでの待ち時間。</summary>
    /// <remarks>1 文字ごとにファイルの有無を調べないため（ネットワーク上のパスは時間がかかる）。</remarks>
    private static readonly TimeSpan PathCheckDelay = TimeSpan.FromMilliseconds(300);

    private readonly LinkMenuService _linkMenu;
    private readonly LinkOpener _opener;
    private readonly IDialogService _dialogs;
    private readonly IFilePickerService _filePicker;
    private readonly IFolderPickerService _folderPicker;

    /// <summary>読み込みに失敗しているか。</summary>
    /// <remarks>失敗したまま保存すると、手修正の誤り等で読めなかった元のファイルを空の構成で上書きして消してしまうため、保存を止める。</remarks>
    private bool _loadFailed;

    private bool _initialized;

    private CancellationTokenSource? _pathCheck;

    public LinkEditorViewModel(
        LinkMenuService linkMenu,
        LinkOpener opener,
        IDialogService dialogs,
        IFilePickerService filePicker,
        IFolderPickerService folderPicker)
    {
        _linkMenu = linkMenu;
        _opener = opener;
        _dialogs = dialogs;
        _filePicker = filePicker;
        _folderPicker = folderPicker;
        ErrorMessage = string.Empty;
        RootItems.CollectionChanged += OnChildrenChanged;
    }

    /// <summary>追加した項目の名前をすぐ入力できるよう、名前欄へのフォーカスを求める。</summary>
    public event EventHandler? EditNameRequested;

    /// <summary>保存が完了した。</summary>
    public event EventHandler? Saved;

    public ObservableCollection<LinkTreeItem> RootItems { get; } = [];

    /// <summary>項目が 1 件も無いか（空のときの案内を出す）。</summary>
    public bool IsEmpty => RootItems.Count == 0;

    /// <summary>未保存の変更があるか。</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand), nameof(DiscardCommand))]
    public partial bool IsDirty { get; set; }

    /// <summary>初回表示時に読み込む。</summary>
    /// <remarks>ページはキャッシュされ表示のたびに呼ばれるので、2 回目以降は何もしない（編集中の内容を読み直して消さないため）。</remarks>
    public async Task InitializeAsync()
    {
        if (_initialized) return;
        _initialized = true;
        await LoadAsync();
    }

    /// <summary>保存済みの構成を読み込んでツリーを作り直す。</summary>
    public async Task LoadAsync()
    {
        LinkMenu menu;
        try
        {
            menu = await _linkMenu.LoadAsync();
        }
        catch (DataFileException ex)
        {
            _loadFailed = true;
            NotifySaveStateChanged();
            ShowError($"{ex.Message}\nファイルを修正して「変更を破棄」で読み直すまで、保存できません。");
            return;
        }

        _loadFailed = false;

        RootItems.Clear();
        foreach (var node in menu.Items!)
        {
            RootItems.Add(LinkTreeItem.From(node));
        }
        SelectedItem = null;
        IsDirty = false;
        NotifySaveStateChanged();
    }

    #region 選択・編集欄

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLinkSelected), nameof(IsFolderSelected), nameof(IsEditorVisible), nameof(IsEditorHintVisible), nameof(EditorHint))]
    [NotifyCanExecuteChangedFor(nameof(DeleteCommand), nameof(OpenSelectedCommand), nameof(BrowseFileCommand), nameof(BrowseFolderCommand))]
    public partial LinkTreeItem? SelectedItem { get; set; }

    public bool IsLinkSelected => SelectedItem?.Kind == LinkItemKind.Link;

    public bool IsFolderSelected => SelectedItem?.Kind == LinkItemKind.Folder;

    /// <summary>編集欄を出すか（リンク・フォルダーを選択中）。</summary>
    public bool IsEditorVisible => IsLinkSelected || IsFolderSelected;

    /// <summary>編集欄の代わりに案内を出すか（未選択・区切り線）。</summary>
    public bool IsEditorHintVisible => !IsEditorVisible;

    public string EditorHint => SelectedItem is null
        ? "ツリーで項目を選ぶと、ここで名前やパスを編集できます。"
        : "区切り線には編集できる項目がありません。ドラッグで位置だけ変えられます。";

    partial void OnSelectedItemChanged(LinkTreeItem? oldValue, LinkTreeItem? newValue)
    {
        if (oldValue is not null)
        {
            oldValue.PropertyChanged -= OnSelectedItemPropertyChanged;
        }
        if (newValue is not null)
        {
            newValue.PropertyChanged += OnSelectedItemPropertyChanged;
        }
        UpdatePathKind();
    }

    private void OnSelectedItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LinkTreeItem.Path))
        {
            OpenSelectedCommand.NotifyCanExecuteChanged();
            UpdatePathKind();
        }
    }

    /// <summary>選択中のリンクのパスが指す先の種類。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PathKindText), nameof(PathKindGlyph), nameof(IsPathNotFound), nameof(IsPathFound))]
    public partial LinkTargetKind PathKind { get; set; }

    public string PathKindText => PathKind switch
    {
        LinkTargetKind.Url => "URL（ブラウザなど既定のアプリで開きます）",
        LinkTargetKind.Folder => "フォルダー（エクスプローラーで開きます）",
        LinkTargetKind.Executable => "実行ファイル（起動します）",
        LinkTargetKind.File => "ファイル（関連付けられたアプリで開きます）",
        LinkTargetKind.NotFound => "見つかりません。パスを確認してください",
        _ => "",
    };

    public string PathKindGlyph => PathKind switch
    {
        LinkTargetKind.Url => "",
        LinkTargetKind.Folder => "",
        LinkTargetKind.Executable => "",
        LinkTargetKind.File => "",
        LinkTargetKind.NotFound => "",
        _ => "",
    };

    public bool IsPathNotFound => PathKind == LinkTargetKind.NotFound;

    public bool IsPathFound => PathKind is not (LinkTargetKind.Empty or LinkTargetKind.NotFound);

    /// <summary>入力が止まるのを待ってから、パスの種類を調べ直す。</summary>
    private async void UpdatePathKind()
    {
        _pathCheck?.Cancel();
        _pathCheck = null;

        if (SelectedItem is not { Kind: LinkItemKind.Link } link || string.IsNullOrWhiteSpace(link.Path))
        {
            PathKind = LinkTargetKind.Empty;
            return;
        }

        var cancellation = _pathCheck = new CancellationTokenSource();
        var path = link.Path;
        try
        {
            await Task.Delay(PathCheckDelay, cancellation.Token);
            var kind = await Task.Run(() => LinkTarget.Classify(path), cancellation.Token);
            if (!cancellation.IsCancellationRequested)
            {
                PathKind = kind;
            }
        }
        catch (OperationCanceledException)
        {
            // 次の入力・選択で調べ直す
        }
    }

    [RelayCommand(CanExecute = nameof(CanOpenSelected))]
    private async Task OpenSelectedAsync()
    {
        try
        {
            await _opener.OpenAsync(SelectedItem!.Path);
        }
        catch (LinkOpenException ex)
        {
            ShowError(ex.Message);
        }
    }

    private bool CanOpenSelected() => IsLinkSelected && !string.IsNullOrWhiteSpace(SelectedItem!.Path);

    [RelayCommand(CanExecute = nameof(IsLinkSelected))]
    private async Task BrowseFileAsync()
    {
        var link = SelectedItem!;
        if (await _filePicker.PickFileAsync() is { } path)
        {
            link.Path = path;
        }
    }

    [RelayCommand(CanExecute = nameof(IsLinkSelected))]
    private async Task BrowseFolderAsync()
    {
        var link = SelectedItem!;
        if (await _folderPicker.PickFolderAsync() is { } path)
        {
            link.Path = path;
        }
    }

    #endregion

    #region 追加・削除

    [RelayCommand]
    private void AddLink() => Add(LinkTreeItem.CreateLink());

    [RelayCommand]
    private void AddFolder() => Add(LinkTreeItem.CreateFolder());

    [RelayCommand]
    private void AddSeparator() => Add(LinkTreeItem.CreateSeparator());

    /// <summary>選択がフォルダならその子の末尾、それ以外なら同じ階層の選択の直後、未選択ならルートの末尾に追加して選択する。</summary>
    private void Add(LinkTreeItem item)
    {
        if (SelectedItem is { } selected)
        {
            if (selected.IsFolder)
            {
                // フォルダは子が増えると自分で開く
                selected.Children!.Add(item);
            }
            else if (FindParentCollection(selected) is { } siblings)
            {
                siblings.Insert(siblings.IndexOf(selected) + 1, item);
            }
            else
            {
                RootItems.Add(item);
            }
        }
        else
        {
            RootItems.Add(item);
        }

        SelectedItem = item;
        if (!item.IsSeparator)
        {
            EditNameRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>選択を削除する。</summary>
    /// <remarks>
    /// 中身のあるフォルダだけ確認する（配下のリンクをまとめて失うため）。リンク・区切り線・空のフォルダは 1 件単位で作り直しやすく、
    /// 保存前なら「変更を破棄」でも戻せるので、確認せずに削除する。
    /// </remarks>
    [RelayCommand(CanExecute = nameof(CanDelete))]
    private async Task DeleteAsync()
    {
        if (SelectedItem is not { } selected)
        {
            return;
        }

        var count = selected.CountDescendants();
        if (count > 0 && !await _dialogs.ConfirmAsync(
                $"「{selected.Name}」を削除しますか？",
                $"中の {count} 件の項目もまとめて削除されます。保存する前なら「変更を破棄」で元に戻せます。",
                "削除"))
        {
            return;
        }

        FindParentCollection(selected)?.Remove(selected);
        SelectedItem = null;
    }

    private bool CanDelete() => SelectedItem is not null;

    /// <summary>要素が今入っているコレクション（ルートまたは親フォルダの子要素）を探す。</summary>
    /// <remarks>ドラッグ＆ドロップで並べ替えたあとも、その時点の構造から探す。</remarks>
    public ObservableCollection<LinkTreeItem>? FindParentCollection(LinkTreeItem item)
        => FindParentCollection(RootItems, item);

    private static ObservableCollection<LinkTreeItem>? FindParentCollection(ObservableCollection<LinkTreeItem> items, LinkTreeItem target)
    {
        if (items.Contains(target))
        {
            return items;
        }

        foreach (var item in items)
        {
            if (item.Children is { } children && FindParentCollection(children, target) is { } found)
            {
                return found;
            }
        }
        return null;
    }

    #endregion

    #region 保存・破棄

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        var menu = new LinkMenu { Items = [.. RootItems.Select(item => item.ToNode())] };
        try
        {
            await _linkMenu.SaveAsync(menu);
            IsDirty = false;
            Saved?.Invoke(this, EventArgs.Empty);
        }
        catch (DataFileException ex)
        {
            ShowError(ex.Message);
        }
    }

    private bool CanSave() => IsDirty && !_loadFailed;

    /// <summary>変更を破棄して、保存済みの構成を読み直す。</summary>
    /// <remarks>読み込みに失敗しているときは、ファイルを直したあとの読み直しにも使う。</remarks>
    [RelayCommand(CanExecute = nameof(CanDiscard))]
    private Task DiscardAsync() => LoadAsync();

    private bool CanDiscard() => IsDirty || _loadFailed;

    private void NotifySaveStateChanged()
    {
        SaveCommand.NotifyCanExecuteChanged();
        DiscardCommand.NotifyCanExecuteChanged();
    }

    #endregion

    #region 変更の検知

    // 追加・削除・並べ替え（ドラッグ＆ドロップによるコレクションの変更を含む）と、名前・パスの編集でダーティにする

    private void OnChildrenChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not null)
        {
            foreach (LinkTreeItem item in e.NewItems)
            {
                Track(item);
            }
        }
        if (sender == RootItems)
        {
            OnPropertyChanged(nameof(IsEmpty));
        }
        IsDirty = true;
    }

    private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(LinkTreeItem.Name) or nameof(LinkTreeItem.Path))
        {
            IsDirty = true;
        }
    }

    /// <summary>要素とその子孫の変更を監視する。</summary>
    /// <remarks>並べ替えで同じ要素が再び追加されることがあるため、二重に登録しないよう外してから登録する。</remarks>
    private void Track(LinkTreeItem item)
    {
        item.PropertyChanged -= OnItemPropertyChanged;
        item.PropertyChanged += OnItemPropertyChanged;

        if (item.Children is { } children)
        {
            children.CollectionChanged -= OnChildrenChanged;
            children.CollectionChanged += OnChildrenChanged;
            foreach (var child in children)
            {
                Track(child);
            }
        }
    }

    #endregion

    #region エラー表示

    [ObservableProperty]
    public partial string ErrorMessage { get; set; }

    [ObservableProperty]
    public partial bool IsErrorOpen { get; set; }

    private void ShowError(string message)
    {
        ErrorMessage = message;
        IsErrorOpen = true;
    }

    #endregion
}
