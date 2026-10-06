using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MmmSdk.WinUI.Utilities;

namespace MmmTool.Features.Database.Connection;

/// <summary>DB への接続の入力欄 (設定ページと、初回の保存先の選択の画面で共有する部品)</summary>
/// <remarks>呼び出し側が、自分の ViewModel が持つ <see cref="DatabaseConnectionViewModel"/> を渡して作る (2 つの画面で同じ入力欄を使うため、DI からは作らない)。</remarks>
public sealed partial class DatabaseConnectionForm : UserControl
{
    /// <summary>部品の ViewModel</summary>
    public DatabaseConnectionViewModel ViewModel { get; }

    /// <summary>部品を作る</summary>
    /// <param name="viewModel">部品の ViewModel</param>
    public DatabaseConnectionForm(DatabaseConnectionViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    /// <summary>サーバー名・ユーザー名などにフォーカスが来たら IME をオフにする (半角の英数字を打つため)</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">イベントの情報</param>
    private void OnAsciiBoxGotFocus(object sender, RoutedEventArgs e) => ImeControl.TurnOff();

    /// <summary>パスワードの入力を ViewModel へ写す</summary>
    /// <param name="sender">イベントの送信元 (パスワードの欄)</param>
    /// <param name="e">イベントの情報</param>
    private void OnPasswordChanged(object sender, RoutedEventArgs e) => ViewModel.Password = ((PasswordBox)sender).Password;
}
