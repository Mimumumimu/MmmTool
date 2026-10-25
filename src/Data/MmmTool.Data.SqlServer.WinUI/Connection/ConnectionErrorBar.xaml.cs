using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MmmTool.Data.SqlServer.WinUI.Connection;

/// <summary>DB を読む画面の上に重ねる、エラーの InfoBar</summary>
/// <remarks>画面の ViewModel (<see cref="DatabaseViewModelBase"/>)を渡すと、そのエラーを出し、「接続の設定」ボタンで接続の設定の画面を開く。</remarks>
public sealed partial class ConnectionErrorBar : UserControl
{
    /// <summary><see cref="ViewModel"/> の依存関係プロパティ</summary>
    public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
        nameof(ViewModel), typeof(DatabaseViewModelBase), typeof(ConnectionErrorBar), new PropertyMetadata(null));

    /// <summary>エラーを出す画面の ViewModel</summary>
    public DatabaseViewModelBase? ViewModel
    {
        get => (DatabaseViewModelBase?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    /// <summary>部品を作る</summary>
    public ConnectionErrorBar()
    {
        InitializeComponent();
    }
}
