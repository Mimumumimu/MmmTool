using Microsoft.UI.Dispatching;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using MmmSdk.Core.Utilities;
using MmmSdk.WinUI.Utilities;
using Windows.UI.Core;
using VirtualKey = Windows.System.VirtualKey;

namespace MmmTool.Messages.Main;

/// <summary>サイドバーのメッセージのページ。左に人の一覧、右に会話と入力欄を並べる</summary>
public sealed partial class MessagesPage : Page
{
    /// <summary>メッセージの一覧が先頭に近づいた、と見なす位置 (DIP)</summary>
    private const double NearTopOffset = 80;

    /// <summary>メッセージの一覧が一番下に近い、と見なす残りの高さ (DIP)</summary>
    private const double NearBottomOffset = 120;

    /// <summary>ページが載っている画面。見える・見えないの変化を受けるため。載っていない間は null</summary>
    private XamlRoot? _root;

    /// <summary>メッセージの一覧のスクロールの枠。まだ見つけていなければ null</summary>
    private ScrollViewer? _scroller;

    /// <summary>ページの ViewModel</summary>
    public MessagesViewModel ViewModel { get; }

    /// <summary>ページを作る</summary>
    /// <param name="viewModel">ページの ViewModel</param>
    public MessagesPage(MessagesViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();

        // 読み続ける部品の通知は、任意のスレッドから来る。画面の変更は UI スレッドで行う
        ViewModel.Post = action => DispatcherQueue.TryEnqueue(() => action());
        ViewModel.ScrollToEndRequested += ScrollToEnd;
        ViewModel.ScrollToMessageRequested += ScrollToMessage;

        Loaded += (_, _) =>
        {
            _root = XamlRoot;
            _root.Changed += OnXamlRootChanged;
            UpdateVisible();
            ViewModel.InitializeAsync().Forget();
        };
        Unloaded += (_, _) =>
        {
            if (_root is not null)
            {
                _root.Changed -= OnXamlRootChanged;
                _root = null;
            }
            ViewModel.SetVisible(false);
            ViewModel.Detach();
        };
    }

    /// <summary>メッセージの出る・出さないを、真偽から決める</summary>
    /// <param name="value">出すか</param>
    /// <returns>出すなら Visible、出さないなら Collapsed</returns>
    public static Visibility Visible(bool value) => value ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>ページが載っているウィンドウの表示の状態が変わったら、読む間隔を直す</summary>
    /// <param name="sender">ウィンドウの内容の画面 (<c>XamlRoot</c>)</param>
    /// <param name="args">変更の情報</param>
    /// <remarks>ウィンドウは、× でトレイへ隠す。ページの <c>Unloaded</c> は来ないので、ここで見える・見えないを知る。</remarks>
    private void OnXamlRootChanged(XamlRoot sender, XamlRootChangedEventArgs args) => UpdateVisible();

    /// <summary>ページが見えているかを、いまの画面から決めて、ViewModel に伝える</summary>
    private void UpdateVisible() => ViewModel.SetVisible(_root?.IsHostVisible == true);

    /// <summary>探す欄にフォーカスが来たら、IME をオンにする。日本語の入力が多いため</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">イベントの情報</param>
    private void OnJapaneseBoxGotFocus(object sender, RoutedEventArgs e) => ImeControl.TurnOn();

    /// <summary>会話の一覧の行を押したら、会話を開く</summary>
    /// <param name="sender">一覧</param>
    /// <param name="e">押した行の情報</param>
    private void OnConversationClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is ConversationItem item)
        {
            ViewModel.OpenAsync(item).Forget();
        }
    }

    /// <summary>宛先の人の行を押したら、その人との会話を開く</summary>
    /// <param name="sender">一覧</param>
    /// <param name="e">押した行の情報</param>
    private void OnPersonClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is PersonItem person)
        {
            ViewModel.OpenPersonCommand.Execute(person);
        }
    }

    /// <summary>入力欄で Ctrl+Enter が押されたら、送信する。Enter だけなら改行</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">キー入力の情報</param>
    private void OnBodyPreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter
            && InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control).HasFlag(CoreVirtualKeyStates.Down))
        {
            e.Handled = true;
            if (ViewModel.SendCommand.CanExecute(null))
            {
                ViewModel.SendCommand.Execute(null);
            }
        }
    }

    /// <summary>メッセージの「返信」を押したら、返信を始める</summary>
    /// <param name="sender">押したボタン。<c>Tag</c> に、メッセージの行を持つ</param>
    /// <param name="e">イベントの情報</param>
    private void OnReplyClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: ChatMessageItem message })
        {
            ViewModel.StartReplyCommand.Execute(message);
        }
    }

    /// <summary>メッセージの一覧を作ったら、スクロールの枠を見つけて、先頭に近づいたことを受ける</summary>
    /// <param name="sender">メッセージの一覧</param>
    /// <param name="e">イベントの情報</param>
    private void OnMessageListLoaded(object sender, RoutedEventArgs e)
    {
        if (_scroller is not null)
        {
            return;
        }

        _scroller = FindScroller(MessageList);
        if (_scroller is not null)
        {
            _scroller.ViewChanged += (_, _) =>
            {
                if (_scroller.VerticalOffset < NearTopOffset)
                {
                    ViewModel.LoadOlderAsync().Forget();
                }
            };
        }
    }

    /// <summary>一番下のメッセージまでスクロールする</summary>
    /// <param name="onlyIfNearBottom">true のときは、見ている位置が一番下の近くのときだけ。新着が届いたとき、さかのぼって読んでいる人の位置を動かさないため</param>
    private void ScrollToEnd(bool onlyIfNearBottom) => DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
    {
        if (ViewModel.Messages.Count == 0)
        {
            return;
        }
        if (onlyIfNearBottom && _scroller is { } scroller && scroller.ScrollableHeight - scroller.VerticalOffset > NearBottomOffset)
        {
            return;
        }

        MessageList.ScrollIntoView(ViewModel.Messages[^1]);
    });

    /// <summary>指定のメッセージまでスクロールする。通知を押して開いたとき</summary>
    /// <param name="messageId">メッセージの番号</param>
    private void ScrollToMessage(int messageId) => DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
    {
        if (ViewModel.Messages.FirstOrDefault(item => item.Id == messageId) is { } target)
        {
            MessageList.ScrollIntoView(target, ScrollIntoViewAlignment.Leading);
        }
    });

    /// <summary>一覧の中の、スクロールの枠を探す</summary>
    /// <param name="parent">探す元</param>
    /// <returns>見つけたスクロールの枠。無ければ null</returns>
    private static ScrollViewer? FindScroller(DependencyObject parent)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is ScrollViewer scroller)
            {
                return scroller;
            }
            if (FindScroller(child) is { } found)
            {
                return found;
            }
        }
        return null;
    }
}
