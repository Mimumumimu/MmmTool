using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MmmTool.CodeMaster.Core;

namespace MmmTool.CodeMaster.Settings;

/// <summary>設定ページの「選択肢」で、コードマスタを編集できるときだけ並ぶ部品</summary>
public sealed partial class CodeMasterSettingsControl : UserControl
{
    /// <summary>部品の ViewModel</summary>
    public CodeMasterSettingsViewModel ViewModel { get; }

    /// <summary>部品を作る</summary>
    /// <param name="viewModel">部品の ViewModel</param>
    public CodeMasterSettingsControl(CodeMasterSettingsViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        Visibility = Visibility.Collapsed;
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    /// <summary>画面に載ったら、区分を並べて購読を始める</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">イベントの情報</param>
    private void OnLoaded(object sender, RoutedEventArgs e) => ViewModel.Attach();

    /// <summary>画面から外れたら、購読をやめる</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">イベントの情報</param>
    private void OnUnloaded(object sender, RoutedEventArgs e) => ViewModel.Detach();

    /// <summary>出すか隠すかを、ViewModel に合わせる</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">変わったプロパティの情報</param>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CodeMasterSettingsViewModel.IsVisible))
        {
            Visibility = ViewModel.IsVisible ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    /// <summary>カードが押されたら、その区分の編集画面を開く</summary>
    /// <param name="sender">押されたカード (Tag に区分を持つ)</param>
    /// <param name="e">イベントの情報</param>
    private void OnCardClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: CodeCategoryEditor editor })
        {
            ViewModel.OpenCommand.Execute(editor);
        }
    }
}
