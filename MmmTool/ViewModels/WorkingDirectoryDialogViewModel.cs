using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MmmTool.Core.Repositories;
using MmmTool.Core.Services;
using MmmTool.Services;

namespace MmmTool.ViewModels;

public sealed partial class WorkingDirectoryDialogViewModel : ObservableObject
{
    private readonly CliSettingsService _settings;
    private readonly IFolderPickerService _folderPicker;

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

    public bool HasNoDirectories => Directories.Count == 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsValid), nameof(IsNotFound))]
    public partial string DirectoryPath { get; set; }

    /// <summary>入力されたフォルダが存在する（変更できる）。</summary>
    public bool IsValid => !string.IsNullOrWhiteSpace(DirectoryPath) && Directory.Exists(DirectoryPath.Trim());

    /// <summary>入力はあるがフォルダが見つからない。</summary>
    public bool IsNotFound => !string.IsNullOrWhiteSpace(DirectoryPath) && !IsValid;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string ErrorMessage { get; set; }

    public bool HasError => ErrorMessage.Length > 0;

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

    [RelayCommand]
    private async Task BrowseAsync()
    {
        if (await _folderPicker.PickFolderAsync() is { } directory)
        {
            DirectoryPath = directory;
        }
    }

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
