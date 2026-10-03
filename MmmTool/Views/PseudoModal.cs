using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using MmmTool.Interop;
using Windows.Graphics;

namespace MmmTool.Views;

/// <summary>ウィンドウを親の上に擬似モーダルで出す</summary>
/// <remarks>
/// 表示中は親を操作できないようにし（Win32 のモーダルダイアログと同じく <c>EnableWindow</c> で親を無効にする。<c>OverlappedPresenter.IsModal</c> では親を操作できてしまった）、
/// 閉じるときに親を戻して前面に出す。モーダルの上にさらにモーダルを重ねてもよい（親が自分を無効にし、閉じたら戻す）。
/// </remarks>
internal sealed class PseudoModal
{
    /// <summary>対象のウィンドウ</summary>
    private readonly Window _window;

    /// <summary>親ウィンドウのハンドル。表示するまでは 0</summary>
    private nint _owner;

    /// <summary>親ウィンドウ。表示するまでは null</summary>
    private AppWindow? _ownerWindow;

    /// <summary>対象のウィンドウに付ける</summary>
    public PseudoModal(Window window)
    {
        _window = window;
        // × / Alt+F4 で閉じるとき
        _window.AppWindow.Closing += (_, _) => EnableOwner();
        _window.Closed += OnClosed;
    }

    /// <summary>親ウィンドウの表示倍率</summary>
    public double OwnerScale { get; private set; } = 1.0;

    /// <summary>親を設定する（表示の前に呼ぶ）</summary>
    /// <remarks>常に親の手前に出るようにする。表示倍率も親に合わせて取っておく（初期サイズを決めるため）。</remarks>
    public void SetOwner(Window owner)
    {
        _owner = Win32Interop.GetWindowFromWindowId(owner.AppWindow.Id);
        _ownerWindow = owner.AppWindow;
        OwnerScale = owner.Content?.XamlRoot?.RasterizationScale ?? 1.0;
        NativeMethods.SetOwner(Win32Interop.GetWindowFromWindowId(_window.AppWindow.Id), _owner);
    }

    /// <summary>表示して、親を操作できないようにする</summary>
    public void Show()
    {
        _window.Activate();
        if (_owner != 0)
        {
            NativeMethods.EnableWindow(_owner, false);
        }
    }

    /// <summary>親の中央に置く（作業領域からはみ出す分は内側へ寄せる）</summary>
    public void CenterOnOwner()
    {
        if (_ownerWindow is null)
        {
            return;
        }

        var size = _window.AppWindow.Size;
        var x = _ownerWindow.Position.X + (_ownerWindow.Size.Width - size.Width) / 2;
        var y = _ownerWindow.Position.Y + (_ownerWindow.Size.Height - size.Height) / 2;

        var workArea = DisplayArea.GetFromWindowId(_ownerWindow.Id, DisplayAreaFallback.Nearest).WorkArea;
        x = Math.Clamp(x, workArea.X, Math.Max(workArea.X, workArea.X + workArea.Width - size.Width));
        y = Math.Clamp(y, workArea.Y, Math.Max(workArea.Y, workArea.Y + workArea.Height - size.Height));
        _window.AppWindow.Move(new PointInt32(x, y));
    }

    /// <summary>コードから閉じる（OK・キャンセル等）</summary>
    /// <remarks><c>Window.Close()</c> では <c>AppWindow.Closing</c> が来ないので、閉じる前にここで親を戻す。</remarks>
    public void Close()
    {
        EnableOwner();
        _window.Close();
    }

    /// <summary>閉じたら親を戻して前面に出す</summary>
    private void OnClosed(object sender, WindowEventArgs args)
    {
        EnableOwner();
        if (_owner != 0)
        {
            NativeMethods.SetForegroundWindow(_owner);
        }
    }

    /// <summary>親を操作できる状態に戻す</summary>
    /// <remarks>
    /// 自分が消える前に戻す（無効のままだと、閉じたあとに親ではなく別のアプリが前面に来るため）。
    /// 閉じ方（コードから・×・Alt+F4）によって通る場所が違うので、何度呼んでもよい。
    /// </remarks>
    private void EnableOwner()
    {
        if (_owner != 0)
        {
            NativeMethods.EnableWindow(_owner, true);
        }
    }
}
