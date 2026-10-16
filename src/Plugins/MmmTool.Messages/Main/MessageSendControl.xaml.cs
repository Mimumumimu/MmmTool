using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MmmSdk.WinUI.Utilities;

namespace MmmTool.Messages.Main;

/// <summary>メッセージ送信の入力欄 (サイドバーのページと、トレイから開くウィンドウで共有する)</summary>
public sealed partial class MessageSendControl : UserControl
{
    /// <summary>入力欄の ViewModel (ホストが、作ったあとに渡す)</summary>
    public MessageSendViewModel ViewModel { get; set; } = null!;

    /// <summary>本文欄の最小の高さ (DIP)。本文欄は、余った高さまで伸びる。高さが決まっていないホスト (ページ)は、この高さになる</summary>
    public double BodyMinHeight
    {
        get => BodyBox.MinHeight;
        set => BodyBox.MinHeight = value;
    }

    /// <summary>入力欄を作る</summary>
    public MessageSendControl()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            ViewModel.BodyReset -= OnBodyReset;
            ViewModel.BodyReset += OnBodyReset;
            await ViewModel.InitializeAsync();
        };
        Unloaded += (_, _) => ViewModel.BodyReset -= OnBodyReset;
    }

    /// <summary>本文が署名だけの状態になったら、カーソルを本文の先頭に置く (署名の上に続けて書けるようにする)</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">イベントの情報</param>
    private void OnBodyReset(object? sender, EventArgs e) => BodyBox.SelectionStart = 0;

    /// <summary>タイトル・本文にフォーカスが来たら IME をオンにする (日本語の入力が多いため)</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">イベントの情報</param>
    private void OnJapaneseBoxGotFocus(object sender, RoutedEventArgs e) => ImeControl.TurnOn();

    /// <summary>結果の InfoBar の種類</summary>
    /// <param name="isError">失敗か</param>
    /// <returns>失敗ならエラー、成功なら成功</returns>
    public static InfoBarSeverity ResultSeverity(bool isError) => isError ? InfoBarSeverity.Error : InfoBarSeverity.Success;
}
