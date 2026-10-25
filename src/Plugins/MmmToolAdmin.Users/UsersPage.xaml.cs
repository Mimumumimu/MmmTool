using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MmmSdk.Core.Utilities;

namespace MmmToolAdmin.Users;

/// <summary>ユーザーのページで、一覧と、選んだ人の登録用コードの発行を出す</summary>
/// <remarks>ページを出すたびに、一覧を読み直す。出していない間に変わった分を、すぐ見せるため。</remarks>
public sealed partial class UsersPage : Page
{
    /// <summary>ページの ViewModel</summary>
    public UsersViewModel ViewModel { get; }

    /// <summary>ページを作る</summary>
    /// <param name="viewModel">ページの ViewModel</param>
    public UsersPage(UsersViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        Loaded += OnLoaded;
    }

    /// <summary>ページを出したら、一覧を読み直す</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">イベントの情報</param>
    private void OnLoaded(object sender, RoutedEventArgs e) => ViewModel.RefreshAsync().Forget();
}
