using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MmmSdk.Core.Repositories;
using MmmTool.Core.Repositories;
using MmmTool.Core.Services;
using MmmTool.Services;

namespace MmmTool.ViewModels;

/// <summary>作業ディレクトリ変更ダイアログの ViewModel</summary>
public sealed partial class WorkingDirectoryDialogViewModel : ObservableObject
{
    /// <summary>CLI補助の利用状態</summary>
    private readonly CliSettingsService _settings;
    /// <summary>フォルダ選択</summary>
    private readonly IFolderPickerService _folderPicker;

    /// <summary>ViewModel を作る</summary>
    public WorkingDirectoryDialogViewModel(CliSettingsService settings, IFolderPickerService folderPicker)
    {
        _settings = settings;
        _folderPicker = folderPicker;
        DirectoryPath = string.Empty;
        ErrorMessage = string.Empty;
        Directories.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasNoDirectories));
    }

    /// <summary>最近使ったフォルダ（先頭が最新）。</summary>
    public ObservableCollection<string> Directories { get; } = [];

    /// <summary>最近使ったフォルダが無いか</summary>
    public bool HasNoDirectories => Directories.Count == 0;

    /// <summary>入力されたフォルダのパス</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsValid), nameof(IsNotFound))]
    public partial string DirectoryPath { get; set; }

    /// <summary>入力されたフォルダが存在する（変更できる）。</summary>
    public bool IsValid => !string.IsNullOrWhiteSpace(DirectoryPath) && Directory.Exists(DirectoryPath.Trim());

    /// <summary>入力はあるがフォルダが見つからない。</summary>
    public bool IsNotFound => !string.IsNullOrWhiteSpace(DirectoryPath) && !IsValid;

    /// <summary>エラーメッセージ</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string ErrorMessage { get; set; }

    /// <summary>エラーがあるか</summary>
    public bool HasError => ErrorMessage.Length > 0;

    /// <summary>履歴と最後のディレクトリを読み込んで、表示を初期化する</summary>
    public void Initialize()
    {
        Directories.Clear();
        foreach (var directory in _settings.DirectoryHistory)
        {
            Directories.Add(directory);
        }
        DirectoryPath = _settings.LastDirectory ?? string.Empty;
        ErrorMessage = string.Empty;
    }

    /// <summary>フォルダ選択を開いて、選ばれたフォルダを入力欄に入れる</summary>
    [RelayCommand]
    private async Task BrowseAsync()
    {
        if (await _folderPicker.PickFolderAsync() is { } directory)
        {
            DirectoryPath = directory;
        }
    }

    /// <summary>履歴から取り除く</summary>
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
            ErrorMessage = ex.Message;
        }
    }
}
