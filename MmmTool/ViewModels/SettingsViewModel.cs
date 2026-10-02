using CommunityToolkit.Mvvm.ComponentModel;

namespace MmmTool.ViewModels;

/// <summary>設定ページの ViewModel</summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    /// <summary>ページのタイトル</summary>
    public string Title => "設定";
}
