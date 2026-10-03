using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MmmSdk.WinUI.VisualTree;

namespace MmmTool.Features.Settings;

/// <summary>設定ページ</summary>
public sealed partial class SettingsPage : Page
{
    /// <summary>ページの ViewModel</summary>
    public SettingsViewModel ViewModel { get; }

    /// <summary>ページを作る</summary>
    /// <param name="viewModel">ページの ViewModel</param>
    public SettingsPage(SettingsViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    /// <summary>読み込み時に消去ボタン（×）を隠す</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">イベントの情報</param>
    private void OnNumberBoxLoaded(object sender, RoutedEventArgs e) => HideDeleteButton((DependencyObject)sender);

    /// <summary>フォーカスされたときにも消去ボタン（×）を隠す</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">イベントの情報</param>
    /// <remarks>内部の TextBox のテンプレートが読み込み時にはまだ展開されていないことがあるため、×が出る直前のフォーカス時にもやり直す。</remarks>
    private void OnNumberBoxGotFocus(object sender, RoutedEventArgs e) => HideDeleteButton((DependencyObject)sender);

    /// <summary>NumberBox の消去ボタン（×）を出さない</summary>
    /// <param name="numberBox">対象の NumberBox</param>
    /// <remarks>
    /// NumberBox は消去ボタンを隠すプロパティを持たない。ビジュアルステートは Visibility を書き換えるので、
    /// ステートに触られない大きさ・不透明度・当たり判定で見えなくする（スタイルの MinWidth があるので MinWidth も 0 にする）。
    /// 空欄は <see cref="SettingsViewModel"/> が補正するので消去は不要。
    /// </remarks>
    private static void HideDeleteButton(DependencyObject numberBox)
    {
        if (VisualTreeSearch.FindDescendant<Button>(numberBox, "DeleteButton") is { } button)
        {
            button.MinWidth = 0;
            button.Width = 0;
            button.Opacity = 0;
            button.IsHitTestVisible = false;
        }
    }
}
