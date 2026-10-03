using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MmmSdk.Core.Paths;
using MmmSdk.Core.Storage;
using MmmTool.Core.Links;
using MmmTool.Services;

namespace MmmTool.Features.Links;

/// <summary>
/// リンク編集ページ。リンクメニューのツリーを編集して保存する。
/// </summary>
public sealed partial class LinkEditorViewModel : ObservableObject
{
    /// <summary>パスの入力が止まってから種類を調べるまでの待ち時間。</summary>
    /// <remarks>1 文字ごとにファイルの有無を調べないため（ネットワーク上のパスは時間がかかる）。</remarks>
    private static readonly TimeSpan PathCheckDelay = TimeSpan.FromMilliseconds(300);

    /// <summary>リンクメニューの読み書き</summary>
    private readonly LinkMenuService _linkMenu;
    /// <summary>リンクを開く処理</summary>
    private readonly PathOpener _opener;
    /// <summary>ダイアログ</summary>
    private readonly IDialogService _dialogs;
    /// <summary>ファイル選択</summary>
    private readonly IFilePickerService _filePicker;
    /// <summary>フォルダ選択</summary>
    private readonly IFolderPickerService _folderPicker;

    /// <summary>読み込みに失敗しているか。</summary>
    /// <remarks>失敗したまま保存すると、手修正の誤り等で読めなかった元のファイルを空の構成で上書きして消してしまうため、保存を止める。</remarks>
    private bool _loadFailed;

    /// <summary>初期化済みか</summary>
    private bool _initialized;

    /// <summary>パスの種類を調べる処理の取り消し用</summary>
    private CancellationTokenSource? _pathCheck;

    /// <summary>ViewModel を作る</summary>
    /// <param name="linkMenu">リンクメニューの読み書き</param>
    /// <param name="opener">パスを開く処理</param>
    /// <param name="dialogs">ダイアログを開く</param>
    /// <param name="filePicker">ファイル選択</param>
    /// <param name="folderPicker">フォルダ選択</param>
    public LinkEditorViewModel(
        LinkMenuService linkMenu,
        PathOpener opener,
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

    /// <summary>ツリーの最上位の要素</summary>
    public ObservableCollection<LinkTreeItem> RootItems { get; } = [];

    /// <summary>項目が 1 件も無いか</summary>
    /// <remarks>空のときの案内を出す。</remarks>
    public bool IsEmpty => RootItems.Count == 0;

    /// <summary>未保存の変更があるか。</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand), nameof(DiscardCommand))]
    public partial bool IsDirty { get; set; }

    /// <summary>初回表示時に読み込む。</summary>
    /// <returns>初回の読み込みの完了を表すタスク</returns>
    /// <remarks>ページはキャッシュされ表示のたびに呼ばれるので、2 回目以降は何もしない（編集中の内容を読み直して消さないため）。</remarks>
    public async Task InitializeAsync()
    {
        if (_initialized) return;
        _initialized = true;
        await LoadAsync();

        // 起動時の読み込みで作り直した場合もここで知らせる（読み込みエラーを表示中なら、そちらを優先）
        if (_linkMenu.RecoveryMessage is { } recoveryMessage && !_loadFailed)
        {
            ShowError(recoveryMessage);
        }
    }

    /// <summary>保存済みの構成を読み込んでツリーを作り直す。</summary>
    /// <returns>読み込みの完了を表すタスク</returns>
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

    /// <summary>選択中の要素</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLinkSelected), nameof(IsFolderSelected), nameof(IsEditorVisible), nameof(IsEditorHintVisible), nameof(EditorHint))]
    [NotifyCanExecuteChangedFor(nameof(DeleteCommand), nameof(OpenSelectedCommand), nameof(BrowseFileCommand), nameof(BrowseFolderCommand))]
    public partial LinkTreeItem? SelectedItem { get; set; }

    /// <summary>リンクを選択中か</summary>
    public bool IsLinkSelected => SelectedItem?.Kind == LinkItemKind.Link;

    /// <summary>フォルダーを選択中か</summary>
    public bool IsFolderSelected => SelectedItem?.Kind == LinkItemKind.Folder;

    /// <summary>編集欄を出すか（リンク・フォルダーを選択中）。</summary>
    public bool IsEditorVisible => IsLinkSelected || IsFolderSelected;

    /// <summary>編集欄の代わりに案内を出すか（未選択・区切り線）。</summary>
    public bool IsEditorHintVisible => !IsEditorVisible;

    /// <summary>編集欄の代わりに出す案内の文</summary>
    public string EditorHint => SelectedItem is null
        ? "ツリーで項目を選ぶと、ここで名前やパスを編集できます。"
        : "区切り線には編集できる項目がありません。ドラッグで位置だけ変えられます。";

    /// <summary>選択が変わったら、変更の監視を付け替えて、パスの種類を調べ直す</summary>
    /// <param name="oldValue">変更前の選択</param>
    /// <param name="newValue">変更後の選択</param>
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

    /// <summary>選択中の要素のパスが変わったら、種類を調べ直す</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">変更されたプロパティの情報</param>
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
    public partial PathTargetKind PathKind { get; set; }

    /// <summary>パスの種類の説明</summary>
    public string PathKindText => PathKind switch
    {
        PathTargetKind.Url => "URL（ブラウザなど既定のアプリで開きます）",
        PathTargetKind.Folder => "フォルダー（エクスプローラーで開きます）",
        PathTargetKind.Executable => "実行ファイル（起動します）",
        PathTargetKind.File => "ファイル（関連付けられたアプリで開きます）",
        PathTargetKind.NotFound => "見つかりません。パスを確認してください",
        _ => "",
    };

    /// <summary>パスの種類のアイコン</summary>
    public string PathKindGlyph => PathKind switch
    {
        PathTargetKind.Url => "",
        PathTargetKind.Folder => "",
        PathTargetKind.Executable => "",
        PathTargetKind.File => "",
        PathTargetKind.NotFound => "",
        _ => "",
    };

    /// <summary>パスが見つからないか</summary>
    public bool IsPathNotFound => PathKind == PathTargetKind.NotFound;

    /// <summary>パスが見つかったか（空・見つからない以外）</summary>
    public bool IsPathFound => PathKind is not (PathTargetKind.Empty or PathTargetKind.NotFound);

    /// <summary>入力が止まるのを待ってから、パスの種類を調べ直す。</summary>
    private async void UpdatePathKind()
    {
        _pathCheck?.Cancel();
        _pathCheck = null;

        if (SelectedItem is not { Kind: LinkItemKind.Link } link || string.IsNullOrWhiteSpace(link.Path))
        {
            PathKind = PathTargetKind.Empty;
            return;
        }

        var cancellation = _pathCheck = new CancellationTokenSource();
        var path = link.Path;
        try
        {
            await Task.Delay(PathCheckDelay, cancellation.Token);
            var kind = await Task.Run(() => PathTarget.Classify(path), cancellation.Token);
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

    /// <summary>選択中のリンクを開く</summary>
    /// <returns>リンクを開く処理の完了を表すタスク</returns>
    [RelayCommand(CanExecute = nameof(CanOpenSelected))]
    private async Task OpenSelectedAsync()
    {
        try
        {
            await _opener.OpenAsync(SelectedItem!.Path);
        }
        catch (PathOpenException ex)
        {
            ShowError(ex.Message);
        }
    }

    /// <summary>選択中のリンクを開けるか</summary>
    /// <returns>開けるなら true</returns>
    private bool CanOpenSelected() => IsLinkSelected && !string.IsNullOrWhiteSpace(SelectedItem!.Path);

    /// <summary>ファイル選択を開いて、選ばれたパスを入れる</summary>
    /// <returns>ファイル選択の完了を表すタスク</returns>
    [RelayCommand(CanExecute = nameof(IsLinkSelected))]
    private async Task BrowseFileAsync()
    {
        var link = SelectedItem!;
        if (await _filePicker.PickFileAsync() is { } path)
        {
            link.Path = path;
        }
    }

    /// <summary>フォルダ選択を開いて、選ばれたパスを入れる</summary>
    /// <returns>フォルダ選択の完了を表すタスク</returns>
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

    /// <summary>リンクを追加する</summary>
    [RelayCommand]
    private void AddLink() => Add(LinkTreeItem.CreateLink());

    /// <summary>フォルダーを追加する</summary>
    [RelayCommand]
    private void AddFolder() => Add(LinkTreeItem.CreateFolder());

    /// <summary>区切り線を追加する</summary>
    [RelayCommand]
    private void AddSeparator() => Add(LinkTreeItem.CreateSeparator());

    /// <summary>項目を追加して選択する</summary>
    /// <param name="item">追加する要素</param>
    /// <remarks>選択がフォルダならその子の末尾、それ以外なら同じ階層の選択の直後、未選択ならルートの末尾に追加する。</remarks>
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
    /// <returns>削除の完了を表すタスク</returns>
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

    /// <summary>削除できるか</summary>
    /// <returns>削除できるなら true</returns>
    private bool CanDelete() => SelectedItem is not null;

    /// <summary>要素が今入っているコレクション（ルートまたは親フォルダの子要素）を探す。</summary>
    /// <param name="item">探す要素</param>
    /// <returns>要素が入っているコレクション。見つからなければ null</returns>
    /// <remarks>ドラッグ＆ドロップで並べ替えたあとも、その時点の構造から探す。</remarks>
    public ObservableCollection<LinkTreeItem>? FindParentCollection(LinkTreeItem item)
        => FindParentCollection(RootItems, item);

    /// <summary>要素が入っているコレクションを、指定のコレクションの下から探す</summary>
    /// <param name="items">探し始めるコレクション</param>
    /// <param name="target">探す要素</param>
    /// <returns>要素が入っているコレクション。見つからなければ null</returns>
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

    /// <summary>保存する</summary>
    /// <returns>保存の完了を表すタスク</returns>
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

    /// <summary>保存できるか</summary>
    /// <returns>保存できるなら true</returns>
    private bool CanSave() => IsDirty && !_loadFailed;

    /// <summary>変更を破棄して、保存済みの構成を読み直す。</summary>
    /// <returns>読み直しの完了を表すタスク</returns>
    /// <remarks>読み込みに失敗しているときは、ファイルを直したあとの読み直しにも使う。</remarks>
    [RelayCommand(CanExecute = nameof(CanDiscard))]
    private Task DiscardAsync() => LoadAsync();

    /// <summary>破棄できるか</summary>
    /// <returns>破棄できるなら true</returns>
    private bool CanDiscard() => IsDirty || _loadFailed;

    /// <summary>保存・破棄ボタンの状態を更新する</summary>
    private void NotifySaveStateChanged()
    {
        SaveCommand.NotifyCanExecuteChanged();
        DiscardCommand.NotifyCanExecuteChanged();
    }

    #endregion

    #region 変更の検知

    // 追加・削除・並べ替え（ドラッグ＆ドロップによるコレクションの変更を含む）と、名前・パスの編集でダーティにする

    /// <summary>コレクションが変わったら、新しい要素を監視して、変更ありにする</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">コレクションの変更の情報</param>
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

    /// <summary>名前・パスが変わったら、変更ありにする</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">変更されたプロパティの情報</param>
    private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(LinkTreeItem.Name) or nameof(LinkTreeItem.Path))
        {
            IsDirty = true;
        }
    }

    /// <summary>要素とその子孫の変更を監視する。</summary>
    /// <param name="item">監視する要素</param>
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

    /// <summary>エラーメッセージ</summary>
    [ObservableProperty]
    public partial string ErrorMessage { get; set; }

    /// <summary>エラーを表示中か</summary>
    [ObservableProperty]
    public partial bool IsErrorOpen { get; set; }

    /// <summary>エラーを表示する</summary>
    /// <param name="message">表示するエラーメッセージ</param>
    private void ShowError(string message)
    {
        ErrorMessage = message;
        IsErrorOpen = true;
    }

    #endregion
}
