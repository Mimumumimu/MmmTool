using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace MmmTool.Views.Controls;

/// <summary>押すとリンクを開く領域</summary>
/// <remarks>
/// <see cref="IsLinkEnabled"/> のときだけ、マウスを乗せると手の形のカーソルと背景で押せることを示す。押したときの処理は <c>Tapped</c> で受ける側が行う。
/// カーソルの形（<c>ProtectedCursor</c>）は派生クラスからしか変えられないため、<see cref="Grid"/> を派生させている。
/// </remarks>
public sealed partial class LinkArea : Grid
{
    /// <summary><see cref="IsLinkEnabled"/> の依存関係プロパティ</summary>
    public static readonly DependencyProperty IsLinkEnabledProperty = DependencyProperty.Register(
        nameof(IsLinkEnabled), typeof(bool), typeof(LinkArea), new PropertyMetadata(false, (d, _) => ((LinkArea)d).UpdateLook()));

    /// <summary>マウスが乗っているか</summary>
    private bool _isPointerOver;

    /// <summary>領域を作る</summary>
    public LinkArea()
    {
        // 文字の無い所でもマウス・タップを受けるため、透明で塗る
        Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        PointerEntered += (_, _) => SetPointerOver(true);
        PointerExited += (_, _) => SetPointerOver(false);
        PointerCanceled += (_, _) => SetPointerOver(false);
        PointerCaptureLost += (_, _) => SetPointerOver(false);
    }

    /// <summary>押してリンクを開けるか</summary>
    public bool IsLinkEnabled
    {
        get => (bool)GetValue(IsLinkEnabledProperty);
        set => SetValue(IsLinkEnabledProperty, value);
    }

    /// <summary>マウスが乗っているかを変えて、見た目を合わせる</summary>
    /// <param name="value">マウスが乗っているか</param>
    private void SetPointerOver(bool value)
    {
        _isPointerOver = value;
        UpdateLook();
    }

    /// <summary>カーソルと背景を、押せるか・マウスが乗っているかに合わせる</summary>
    private void UpdateLook()
    {
        ProtectedCursor = IsLinkEnabled ? InputSystemCursor.Create(InputSystemCursorShape.Hand) : null;
        Background = IsLinkEnabled && _isPointerOver
            ? (Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"]
            : new SolidColorBrush(Microsoft.UI.Colors.Transparent);
    }
}
