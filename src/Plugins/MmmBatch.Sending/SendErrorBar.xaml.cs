using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MmmBatch.Sending;

/// <summary>送信の一覧の上に重ねる、エラーの InfoBar (機能ごとの一覧で共通)</summary>
/// <remarks>画面の ViewModel (<see cref="SendViewModelBase"/>)を渡すと、そのエラーを出し、「接続の設定」ボタンで接続の設定の画面を開く。</remarks>
public sealed partial class SendErrorBar : UserControl
{
    /// <summary><see cref="ViewModel"/> の依存関係プロパティ</summary>
    public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
        nameof(ViewModel), typeof(SendViewModelBase), typeof(SendErrorBar), new PropertyMetadata(null));

    /// <summary>エラーを出す画面の ViewModel</summary>
    public SendViewModelBase? ViewModel
    {
        get => (SendViewModelBase?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    /// <summary>部品を作る</summary>
    public SendErrorBar()
    {
        InitializeComponent();
    }
}
