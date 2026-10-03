using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MmmSdk.Core.Storage;
using MmmSdk.Core.Tasks;
using MmmSdk.WinUI.Dialogs;
using MmmSdk.WinUI.Errors;
using MmmTool.Core.CliAssist;

namespace MmmTool.Features.CliAssist.WorkingDirectory;

/// <summary>作業ディレクトリ変更ダイアログの ViewModel</summary>
public sealed partial class WorkingDirectoryDialogViewModel : ObservableObject
{
    /// <summary>CLI補助の利用状態</summary>
    private readonly CliSettingsService _settings;
    /// <summary>フォルダ選択</summary>
    private readonly IFolderPickerService _folderPicker;

    /// <summary>入力が止まってから存在を確認するまでの待ち時間</summary>
    private static readonly TimeSpan CheckDelay = TimeSpan.FromMilliseconds(250);

    /// <summary>入力されたフォルダの確認状態</summary>
    private DirectoryCheck _check;

    /// <summary>存在の確認の待ち合わせ（入力が変わったら、前の確認を取り消す）</summary>
    private readonly Debouncer _checkDebouncer = new(CheckDelay);

    /// <summary>ViewModel を作る</summary>
    /// <param name="settings">CLI補助の利用状態</param>
    /// <param name="folderPicker">フォルダ選択</param>
    public WorkingDirectoryDialogViewModel(CliSettingsService settings, IFolderPickerService folderPicker)
    {
        _settings = settings;
        _folderPicker = folderPicker;
        DirectoryPath = string.Empty;
        Directories.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasNoDirectories));
    }

    /// <summary>最近使ったフォルダ（先頭が最新）。</summary>
    public ObservableCollection<string> Directories { get; } = [];

    /// <summary>最近使ったフォルダが無いか</summary>
    public bool HasNoDirectories => Directories.Count == 0;

    /// <summary>入力されたフォルダのパス</summary>
    [ObservableProperty]
    public partial string DirectoryPath { get; set; }

    /// <summary>入力されたフォルダが存在する（変更できる）。</summary>
    /// <remarks>存在の確認は、入力が止まってから、バックグラウンドで行う。確認が終わるまでは false。</remarks>
    public bool IsValid => _check == DirectoryCheck.Found;

    /// <summary>入力はあるがフォルダが見つからない。</summary>
    /// <remarks>確認が終わるまでは false（確認中に、見つからないと表示して、ちらつかせないため）。</remarks>
    public bool IsNotFound => _check == DirectoryCheck.NotFound;

    /// <summary>エラー</summary>
    public ErrorState Error { get; } = new();

    /// <summary>履歴のフォルダを入力欄に入れて、存在するかを（デバウンスを待たずに）確認する</summary>
    /// <param name="directory">履歴のフォルダのパス</param>
    /// <returns>存在すれば true（ダブルクリックで、そのまま決定してよいか）</returns>
    /// <remarks>存在の確認は、UI スレッドを止めないよう、バックグラウンドで行う。</remarks>
    public async Task<bool> UseDirectoryAsync(string directory)
    {
        DirectoryPath = directory;
        var trimmed = directory.Trim();
        var exists = trimmed.Length > 0 && await Task.Run(() => Directory.Exists(trimmed));
        return exists && DirectoryPath == directory;
    }

    /// <summary>入力が変わったら、存在の確認をやり直す</summary>
    /// <param name="value">変更後のパス</param>
    partial void OnDirectoryPathChanged(string value) => UpdateCheck(value);

    /// <summary>入力が止まるのを待ってから、フォルダの存在をバックグラウンドで確認する</summary>
    /// <param name="path">入力されたパス</param>
    /// <remarks>
    /// ネットワークパスなどで、存在の確認が長く止まることがあるので、UI スレッドでは呼ばない。
    /// 入力のたびに呼ぶと負荷になるので、入力が止まってから 1 回だけ確認する。
    /// </remarks>
    private async void UpdateCheck(string path)
    {
        var trimmed = path.Trim();
        if (trimmed.Length == 0)
        {
            _checkDebouncer.Cancel();
            SetCheck(DirectoryCheck.None);
            return;
        }

        SetCheck(DirectoryCheck.Checking);
        await _checkDebouncer.RunAsync(
            () => Directory.Exists(trimmed),
            exists => SetCheck(exists ? DirectoryCheck.Found : DirectoryCheck.NotFound));
    }

    /// <summary>確認状態を変えて、表示を更新する</summary>
    /// <param name="check">新しい確認状態</param>
    private void SetCheck(DirectoryCheck check)
    {
        _check = check;
        OnPropertyChanged(nameof(IsValid));
        OnPropertyChanged(nameof(IsNotFound));
    }

    /// <summary>入力されたフォルダの確認状態</summary>
    private enum DirectoryCheck
    {
        /// <summary>入力が空（確認しない）</summary>
        None,

        /// <summary>確認中</summary>
        Checking,

        /// <summary>フォルダがある</summary>
        Found,

        /// <summary>フォルダが無い（ファイルなど、フォルダでないものを含む）</summary>
        NotFound,
    }

    /// <summary>履歴と最後のディレクトリを読み込んで、表示を初期化する</summary>
    public void Initialize()
    {
        Directories.Clear();
        foreach (var directory in _settings.DirectoryHistory)
        {
            Directories.Add(directory);
        }
        DirectoryPath = _settings.LastDirectory ?? string.Empty;
        Error.Clear();
    }

    /// <summary>フォルダ選択を開いて、選ばれたフォルダを入力欄に入れる</summary>
    /// <returns>選択の完了を表すタスク</returns>
    [RelayCommand]
    private async Task BrowseAsync()
    {
        if (await _folderPicker.PickFolderAsync() is { } directory)
        {
            DirectoryPath = directory;
        }
    }

    /// <summary>履歴から取り除く</summary>
    /// <param name="directory">取り除くフォルダのパス</param>
    /// <returns>取り除きの完了を表すタスク</returns>
    [RelayCommand]
    private async Task RemoveAsync(string directory)
    {
        Directories.Remove(directory);
        try
        {
            await _settings.RemoveDirectoryAsync(directory);
        }
        catch (DataFileException ex)
        {
            Error.Show(ex.Message);
        }
    }
}
