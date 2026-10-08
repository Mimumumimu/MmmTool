using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using MmmSdk.WinUI.Utilities;
using Windows.System;

namespace MmmTool.Data.SqlServer.WinUI.Connection;

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

    /// <summary>入力欄で Enter を押したら、「接続を確認」を行う</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">キーの情報</param>
    /// <remarks>
    /// 接続を確認できるまで、画面の「保存」・「決定」を押せない画面があるので、入力の次の操作として、Enter で確認できるようにする。
    /// 確認の最中 (<see cref="DatabaseConnectionViewModel.IsBusy"/>)は何もしない。
    /// </remarks>
    private async void OnInputKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Enter)
        {
            return;
        }

        e.Handled = true;
        if (ViewModel.TestCommand.CanExecute(null))
        {
            await ViewModel.TestCommand.ExecuteAsync(null);
        }
    }

    /// <summary>パスワードの入力を ViewModel へ写す</summary>
    /// <param name="sender">イベントの送信元 (パスワードの欄)</param>
    /// <param name="e">イベントの情報</param>
    private void OnPasswordChanged(object sender, RoutedEventArgs e) => ViewModel.Password = ((PasswordBox)sender).Password;
}
