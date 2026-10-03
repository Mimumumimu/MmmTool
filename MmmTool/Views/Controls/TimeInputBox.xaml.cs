using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using MmmTool.Interop;
using Windows.System;

namespace MmmTool.Views.Controls;

/// <summary>時刻の入力欄（24 時間。1 つの枠の中に「時 : 分」の 2 区画）</summary>
/// <remarks>
/// 区画を押して直接打つ（2 桁打つと時から分へ移る）か、↑↓ キー・マウスホイールで 1 ずつ増減する（端まで行ったら反対の端へ回る）。
/// 打った値は区画を離れたときに確定し、空なら元の値に戻し、範囲を超えたら最大値に収める。ボタン類は置かない。
/// </remarks>
public sealed partial class TimeInputBox : UserControl
{
    /// <summary><see cref="Hour"/> の依存関係プロパティ</summary>
    public static readonly DependencyProperty HourProperty = DependencyProperty.Register(
        nameof(Hour), typeof(int), typeof(TimeInputBox), new PropertyMetadata(0, OnValueChanged));

    /// <summary><see cref="Minute"/> の依存関係プロパティ</summary>
    public static readonly DependencyProperty MinuteProperty = DependencyProperty.Register(
        nameof(Minute), typeof(int), typeof(TimeInputBox), new PropertyMetadata(0, OnValueChanged));

    /// <summary>時の最大値</summary>
    private const int MaxHour = 23;
    /// <summary>分の最大値</summary>
    private const int MaxMinute = 59;

    /// <summary>ポインタが枠の上にあるか</summary>
    private bool _pointerOver;

    /// <summary>入力欄を作る</summary>
    public TimeInputBox()
    {
        InitializeComponent();

        foreach (var box in (TextBox[])[HourBox, MinuteBox])
        {
            box.BeforeTextChanging += OnBeforeTextChanging;
            box.GotFocus += OnSegmentGotFocus;
            box.LostFocus += OnSegmentLostFocus;
            box.PreviewKeyDown += OnSegmentKeyDown;
            // 入力欄の中のスクロールが先に処理済みにするので、処理済みのものも受ける
            box.AddHandler(PointerWheelChangedEvent, new PointerEventHandler(OnSegmentWheelChanged), true);
        }
        HourBox.TextChanged += OnHourTextChanged;
        ShowValues();
    }

    /// <summary>時（0〜23）</summary>
    public int Hour
    {
        get => (int)GetValue(HourProperty);
        set => SetValue(HourProperty, value);
    }

    /// <summary>分（0〜59）</summary>
    public int Minute
    {
        get => (int)GetValue(MinuteProperty);
        set => SetValue(MinuteProperty, value);
    }

    /// <summary>値が変わったら、区画の表示を合わせる</summary>
    /// <param name="sender">変更されたコントロール</param>
    /// <param name="e">変更の情報</param>
    private static void OnValueChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        => ((TimeInputBox)sender).ShowValues();

    /// <summary>時・分を 2 桁で表示する</summary>
    private void ShowValues()
    {
        HourBox.Text = Hour.ToString("00");
        MinuteBox.Text = Minute.ToString("00");
    }

    /// <summary>数字以外は入力させない</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="args">変更前のテキストの情報</param>
    private void OnBeforeTextChanging(TextBox sender, TextBoxBeforeTextChangingEventArgs args)
        => args.Cancel = !args.NewText.All(char.IsAsciiDigit);

    /// <summary>時を 2 桁打ったら分へ移る</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">イベントの情報</param>
    /// <remarks>フォーカス中のキー入力のときだけ（値の表示を更新したときは移らない）。</remarks>
    private void OnHourTextChanged(object sender, TextChangedEventArgs e)
    {
        if (HourBox.FocusState == FocusState.Keyboard || HourBox.FocusState == FocusState.Pointer)
        {
            if (HourBox.Text.Length == 2 && HourBox.SelectionStart == 2)
            {
                MinuteBox.Focus(FocusState.Keyboard);
            }
        }
    }

    /// <summary>区画にフォーカスが来たら、全選択して上書きで打てるようにし、IME をオフにする</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">イベントの情報</param>
    private void OnSegmentGotFocus(object sender, RoutedEventArgs e)
    {
        ((TextBox)sender).SelectAll();
        NativeMethods.TurnOffImeForFocusedWindow();
        UpdateFrame();
    }

    /// <summary>区画を離れたら、打った値を確定する</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">イベントの情報</param>
    private void OnSegmentLostFocus(object sender, RoutedEventArgs e)
    {
        Commit((TextBox)sender);
        UpdateFrame();
    }

    /// <summary>↑↓ で増減、←→ で区画を移る</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">キー入力の情報</param>
    private void OnSegmentKeyDown(object sender, KeyRoutedEventArgs e)
    {
        var box = (TextBox)sender;
        switch (e.Key)
        {
            case VirtualKey.Up:
                Step(box, 1);
                e.Handled = true;
                break;
            case VirtualKey.Down:
                Step(box, -1);
                e.Handled = true;
                break;
            case VirtualKey.Right when box == HourBox && box.SelectionStart == box.Text.Length && box.SelectionLength == 0:
                MinuteBox.Focus(FocusState.Keyboard);
                e.Handled = true;
                break;
            case VirtualKey.Left when box == MinuteBox && box.SelectionStart == 0 && box.SelectionLength == 0:
                HourBox.Focus(FocusState.Keyboard);
                e.Handled = true;
                break;
        }
    }

    /// <summary>ホイールで増減する（上で増、下で減）</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">ホイールの情報</param>
    private void OnSegmentWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        var delta = e.GetCurrentPoint((UIElement)sender).Properties.MouseWheelDelta;
        if (delta != 0)
        {
            Step((TextBox)sender, delta > 0 ? 1 : -1);
            e.Handled = true;
        }
    }

    /// <summary>枠の余白を押しても、時の区画へフォーカスを移す</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">タップの情報</param>
    private void OnFrameTapped(object sender, TappedRoutedEventArgs e)
    {
        if (ReferenceEquals(e.OriginalSource, Frame))
        {
            HourBox.Focus(FocusState.Pointer);
        }
    }

    /// <summary>ポインタが枠に入った</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">ポインタの情報</param>
    private void OnFramePointerEntered(object sender, PointerRoutedEventArgs e)
    {
        _pointerOver = true;
        UpdateFrame();
    }

    /// <summary>ポインタが枠から出た</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">ポインタの情報</param>
    private void OnFramePointerExited(object sender, PointerRoutedEventArgs e)
    {
        _pointerOver = false;
        UpdateFrame();
    }

    /// <summary>枠の見た目（通常・ポインタ上・フォーカス中）を、標準の入力欄に合わせる</summary>
    private void UpdateFrame()
    {
        var focused = HourBox.FocusState != FocusState.Unfocused || MinuteBox.FocusState != FocusState.Unfocused;
        var key = focused ? "TextControlBackgroundFocused" : _pointerOver ? "TextControlBackgroundPointerOver" : "TextControlBackground";
        Frame.Background = (Brush)Application.Current.Resources[key];
        FocusLine.Visibility = focused ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>区画の値を 1 つ増減する（端を越えたら反対の端へ）</summary>
    /// <param name="box">増減する区画</param>
    /// <param name="delta">増減する量</param>
    private void Step(TextBox box, int delta)
    {
        Commit(box);
        if (box == HourBox)
        {
            Hour = Wrap(Hour + delta, MaxHour);
        }
        else
        {
            Minute = Wrap(Minute + delta, MaxMinute);
        }
        box.SelectAll();
    }

    /// <summary>区画に打った値を確定する（空なら元の値、範囲外は最大値）</summary>
    /// <param name="box">確定する区画</param>
    private void Commit(TextBox box)
    {
        var max = box == HourBox ? MaxHour : MaxMinute;
        var current = box == HourBox ? Hour : Minute;
        var value = int.TryParse(box.Text, out var typed) ? Math.Min(typed, max) : current;

        if (box == HourBox) Hour = value; else Minute = value;
        // 値が同じでも「8」→「08」のように表示を整える
        ShowValues();
    }

    /// <summary>0〜最大値の範囲で回す</summary>
    /// <param name="value">回す値</param>
    /// <param name="max">最大値</param>
    /// <returns>0〜最大値の範囲に収めた値</returns>
    private static int Wrap(int value, int max) => (value % (max + 1) + max + 1) % (max + 1);
}
