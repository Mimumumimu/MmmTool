using Microsoft.UI.Xaml.Controls;
using MmmTool.ViewModels;

namespace MmmTool.Views;

/// <summary>DEBUG ページ（デバッグビルドだけで表示する動作確認用）</summary>
public sealed partial class DebugPage : Page
{
    /// <summary>ページの ViewModel</summary>
    public DebugViewModel ViewModel { get; }

    /// <summary>ページを作る</summary>
    /// <param name="viewModel">ページの ViewModel</param>
    public DebugPage(DebugViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }
}
