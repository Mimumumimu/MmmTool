using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MmmTool.CliAssist.Main;

/// <summary>セッションのタブの帯に出す見出し (タブ名と、確認待ちの印)</summary>
public sealed partial class SessionTabHeader : UserControl
{
    /// <summary>見出しを作る</summary>
    public SessionTabHeader() => InitializeComponent();

    /// <summary>タブ名を決める</summary>
    /// <param name="title">タブ名</param>
    public void SetTitle(string title) => TitleText.Text = title;

    /// <summary>確認待ちの印を出す・隠す</summary>
    /// <param name="awaiting">確認待ちなら true</param>
    public void SetAwaiting(bool awaiting) => AwaitingDot.Visibility = awaiting ? Visibility.Visible : Visibility.Collapsed;
}
