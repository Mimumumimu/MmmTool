using Microsoft.UI.Dispatching;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media.Imaging;
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

    /// <summary>会話を切り替えるとき、前の会話が消える長さ。入ってくるものより短く、始めも終わりもなめらかに消す</summary>
    private static readonly TimeSpan FadeOutDuration = TimeSpan.FromMilliseconds(260);

    /// <summary>会話を切り替えるとき、新しい会話が現れ始めるまでの遅れ。前の会話が薄くなってから、現れ始める</summary>
    private static readonly TimeSpan FadeInDelay = TimeSpan.FromMilliseconds(60);

    /// <summary>会話を切り替えるとき、新しい会話が現れる長さ。入ってくるものは長めに、減速して止まる</summary>
    private static readonly TimeSpan FadeInDuration = TimeSpan.FromMilliseconds(360);

    /// <summary>新しい会話が現れるとき、下から浮かび上がる距離 (DIP)</summary>
    private const float FadeInRise = 10f;

    /// <summary>フェードの変え終わりの通知を待つ、時間への上乗せ。通知が来ないときに打ち切る</summary>
    private static readonly TimeSpan FadeTimeout = TimeSpan.FromMilliseconds(200);

    /// <summary>OS の設定で、画面の動きが切られているかを知るための設定</summary>
    private readonly Windows.UI.ViewManagement.UISettings _uiSettings = new();

    /// <summary>ページが載っている画面。見える・見えないの変化を受けるため。載っていない間は null</summary>
    private XamlRoot? _root;

    /// <summary>メッセージの一覧のスクロールの枠。まだ見つけていなければ null</summary>
    private ScrollViewer? _scroller;

    /// <summary>会話を切り替えるたびに進む番号。古い切り替えのフェードの後始末を、使わないために使う</summary>
    private int _switchVersion;

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
        ViewModel.PrepareSwitchAsync = PrepareSwitchAsync;
        ViewModel.MessagesReplaced += OnMessagesReplaced;
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;

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

    /// <summary>会話の行の名前の太さを、読んでいないかから決める</summary>
    /// <param name="unread">読んでいないメッセージがあるか</param>
    /// <returns>読んでいないときは太字、読んだときは通常の見出しの太さ</returns>
    public static Windows.UI.Text.FontWeight NameWeight(bool unread) => unread ? Microsoft.UI.Text.FontWeights.Bold : Microsoft.UI.Text.FontWeights.SemiBold;

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
    /// <remarks>開いている行の印は、<see cref="MessagesViewModel.SelectedConversation"/> が決める。開いている行を押したときは、何もしない。</remarks>
    private void OnConversationClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is ConversationItem item)
        {
            ViewModel.OpenAsync(item).Forget();
        }
    }

    /// <summary>開いている会話の行が変わったら、その行が見える位置までスクロールする</summary>
    /// <param name="sender">ViewModel</param>
    /// <param name="e">変わったプロパティの情報</param>
    /// <remarks>通知から開いたときなど、行が一覧の見えていない所にあっても、印が見えるようにするため。</remarks>
    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MessagesViewModel.SelectedConversation) && ViewModel.SelectedConversation is { } item)
        {
            ConversationList.ScrollIntoView(item);
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

    /// <summary>会話の切り替えの準備をする。前の会話の見た目を撮って上に重ね、下の一覧を透明にする</summary>
    /// <returns>準備の完了を表すタスク</returns>
    /// <remarks>
    /// 撮った画像は、フェードが終わるまで残す。切り替えが続けて来たときは、撮り直さず、残っている画像をそのまま使う。
    /// 一覧がまだ見えていない (幅か高さが 0)ときは、撮らない。
    /// </remarks>
    private async Task PrepareSwitchAsync()
    {
        var version = ++_switchVersion;
        if (SwitchSnapshot.Source is null && MessageList.IsLoaded && MessageList.ActualWidth > 0 && MessageList.ActualHeight > 0)
        {
            var bitmap = new RenderTargetBitmap();
            await bitmap.RenderAsync(MessageList);
            if (version != _switchVersion)
            {
                return;
            }
            SwitchSnapshot.Source = bitmap;        }

        SetOpacity(SwitchSnapshot, 1f);
        SetOpacity(MessageList, 0f);
        SetRise(MessageList, FadeInRise);
    }

    /// <summary>部品の縦の位置のずれを、動きをつけずに、すぐ指定の値にする。動いているものは止める</summary>
    /// <param name="target">位置をずらす部品</param>
    /// <param name="offset">元の位置からの下へのずれ。単位は DIP で、0 が元の位置</param>
    private static void SetRise(UIElement target, float offset)
    {
        ElementCompositionPreview.SetIsTranslationEnabled(target, true);
        var visual = ElementCompositionPreview.GetElementVisual(target);
        visual.StopAnimation("Translation.Y");
        visual.Properties.InsertVector3("Translation", new System.Numerics.Vector3(0f, offset, 0f));
    }

    /// <summary>部品の濃さを、動きをつけずに、すぐ指定の値にする。動いているフェードは止める</summary>
    /// <param name="target">濃さを変える部品</param>
    /// <param name="value">濃さ。0 が透明、1 が不透明</param>
    private static void SetOpacity(UIElement target, float value)
    {
        var visual = ElementCompositionPreview.GetElementVisual(target);
        visual.StopAnimation("Opacity");
        visual.Opacity = value;
    }

    /// <summary>部品の濃さを、いまの濃さから指定の濃さまで、少しずつ変える</summary>
    /// <param name="target">濃さを変える部品</param>
    /// <param name="to">変えたあとの濃さ。0 が透明、1 が不透明</param>
    /// <param name="duration">変える時間</param>
    /// <param name="delay">変え始めるまでの遅れ</param>
    /// <param name="easing">変わり方の曲線。(x1, y1, x2, y2)の 3 次ベジェ</param>
    /// <param name="rise">指定すると、同じ時間・同じ曲線で、この距離だけ下にずれた位置から元の位置へ浮かび上がらせる (DIP)</param>
    /// <returns>変え終わったことを表すタスク</returns>
    /// <remarks>
    /// 部品の <c>Opacity</c> ではなく、描画の層 (<c>Visual</c>)の濃さを動かす。途中で別のフェードを始めても、いまの濃さから始まる。
    /// 変え終わりの通知が来ないときのため、遅れと時間に少し足した長さで打ち切る。
    /// </remarks>
    private static Task FadeAsync(UIElement target, float to, TimeSpan duration, TimeSpan delay, System.Numerics.Vector4 easing, float? rise = null)
    {
        var visual = ElementCompositionPreview.GetElementVisual(target);
        var compositor = visual.Compositor;
        var curve = compositor.CreateCubicBezierEasingFunction(
            new System.Numerics.Vector2(easing.X, easing.Y),
            new System.Numerics.Vector2(easing.Z, easing.W));

        var animation = compositor.CreateScalarKeyFrameAnimation();
        animation.InsertKeyFrame(1f, to, curve);
        animation.Duration = duration;
        animation.DelayTime = delay;

        var completion = new TaskCompletionSource();
        var batch = compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
        visual.StartAnimation("Opacity", animation);
        if (rise is not null)
        {
            var slide = compositor.CreateScalarKeyFrameAnimation();
            slide.InsertKeyFrame(0f, rise.Value);
            slide.InsertKeyFrame(1f, 0f, curve);
            slide.Duration = duration;
            slide.DelayTime = delay;
            ElementCompositionPreview.SetIsTranslationEnabled(target, true);
            visual.StartAnimation("Translation.Y", slide);
        }
        batch.End();
        batch.Completed += (_, _) => completion.TrySetResult();
        return Task.WhenAny(completion.Task, Task.Delay(delay + duration + FadeTimeout));
    }

    /// <summary>出ていくものの曲線。始めはゆっくり動き出し、終わりもゆっくり止まる。加速だけだと、終わりで急に消える</summary>
    private static readonly System.Numerics.Vector4 FadeOutEasing = new(0.33f, 0f, 0.67f, 1f);

    /// <summary>入ってくるものの曲線。始めは速く、止まる手前で減速する (Fluent の減速)</summary>
    private static readonly System.Numerics.Vector4 DecelerateEasing = new(0.1f, 0.9f, 0.2f, 1f);

    /// <summary>メッセージ欄の中身を差し替えたあと、一番下へ合わせてから、重ねたフェードで切り替える</summary>
    /// <remarks>配置が終わるのを待ってから、スクロールする。配置の前だと、一番下の行が、まだ作られていないため。</remarks>
    private void OnMessagesReplaced() => DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () => CrossFadeAsync().Forget());

    /// <summary>一番下へ合わせ、撮っておいた前の会話を薄くしながら、下の新しい会話を濃くする</summary>
    /// <returns>フェードの完了を表すタスク</returns>
    private async Task CrossFadeAsync()
    {
        if (ViewModel.Messages.Count > 0)
        {
            MessageList.UpdateLayout();
            MessageList.ScrollIntoView(ViewModel.Messages[^1]);
            MessageList.UpdateLayout();
            MessageList.ScrollIntoView(ViewModel.Messages[^1]);
        }

        var version = _switchVersion;
        if (_uiSettings.AnimationsEnabled)
        {
            var fadeIn = FadeAsync(MessageList, 1f, FadeInDuration, FadeInDelay, DecelerateEasing, FadeInRise);
            var fadeOut = FadeAsync(SwitchSnapshot, 0f, FadeOutDuration, TimeSpan.Zero, FadeOutEasing);
            await Task.WhenAll(fadeIn, fadeOut);
        }
        else
        {
            // OS の設定で画面の動きが切られているときは、動きをつけず、すぐ切り替える
            SetOpacity(MessageList, 1f);
            SetRise(MessageList, 0f);
            SetOpacity(SwitchSnapshot, 0f);
        }

        // 次の切り替えが始まっていたら、その切り替えが撮った画像を消さない
        if (version == _switchVersion)
        {
            SwitchSnapshot.Source = null;
        }
    }

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
