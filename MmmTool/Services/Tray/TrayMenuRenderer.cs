using System.Runtime.InteropServices;
using Microsoft.Win32;
using static MmmTool.Interop.NativeMethods;

namespace MmmTool.Services.Tray;

/// <summary>
/// トレイメニューの項目を自分で描く（オーナードロー）。
/// </summary>
/// <remarks>
/// Win32 の標準メニューは文字のフォント・大きさ・色をアプリから変えられないため、項目の大きさの計測と描画を引き受ける。
/// 枠（外周・影）は Windows が描く（ダーク／ライトは uxtheme で合わせてある）。
/// メニューを開くたびに作り、閉じたら破棄する（その時点の DPI・テーマで描くため）。
/// </remarks>
internal sealed unsafe class TrayMenuRenderer : IDisposable
{
    /// <summary>文字のフォント。先頭から順に、入っているものを使う。</summary>
    private static readonly string[] FontFaces = ["BIZ UDGothic", "Yu Gothic UI", "Segoe UI"];

    private const int FontSizePoint = 12;

    /// <summary>サブメニューの矢印に使うアイコンフォント。先頭から順に、入っているものを使う。</summary>
    private static readonly string[] IconFontFaces = ["Segoe Fluent Icons", "Segoe MDL2 Assets"];

    /// <summary>右向きの山形（ChevronRight）。</summary>
    private const string ChevronGlyph = "";

    #region 寸法（96 DPI のときの px）

    private const int PaddingLeft = 36;
    private const int PaddingRight = 24;
    private const int ArrowArea = 32;
    private const int PaddingVertical = 4;
    private const int MinWidth = 140;
    private const int SeparatorHeight = 7;
    private const int SeparatorInset = 8;
    private const int HoverInsetX = 4;
    private const int HoverInsetY = 2;
    private const int HoverRadius = 8;
    private const int ArrowSize = 10;

    #endregion

    /// <summary>項目ごとの描画内容。itemData には「添字 + 1」を入れる（0 は未設定と区別するため）。</summary>
    private readonly List<Entry> _entries = [];

    private readonly double _scale;
    private readonly Palette _palette;
    private readonly nint _font;

    /// <summary>矢印用のフォント。入っていなければ 0（矢印は Windows に描かせる）。</summary>
    private readonly nint _iconFont;
    private readonly nint _backgroundBrush;
    private readonly nint _hoverBrush;
    private readonly nint _separatorBrush;
    private readonly int _lineHeight;

    /// <param name="x">メニューを出す位置（この位置のモニターの DPI で描く）。</param>
    /// <param name="y">メニューを出す位置。</param>
    public TrayMenuRenderer(int x, int y)
    {
        var monitor = MonitorFromPoint(new POINT { x = x, y = y }, MONITOR_DEFAULTTONEAREST);
        var dpi = GetDpiForMonitor(monitor, MDT_EFFECTIVE_DPI, out var dpiX, out _) == 0 ? dpiX : GetDpiForSystem();
        _scale = dpi / 96.0;

        _palette = IsDarkMode() ? Palette.Dark : Palette.Light;
        _backgroundBrush = CreateSolidBrush(_palette.Background);
        _hoverBrush = CreateSolidBrush(_palette.Hover);
        _separatorBrush = CreateSolidBrush(_palette.Separator);

        _font = CreateFont(FindInstalledFont(FontFaces) ?? FontFaces[^1], -(int)Math.Round(FontSizePoint * dpi / 72.0));
        _iconFont = FindInstalledFont(IconFontFaces) is { } iconFace ? CreateFont(iconFace, -Px(ArrowSize)) : 0;
        _lineHeight = MeasureText("Ag").Height;
    }

    #region 項目の追加

    /// <summary>コマンドの項目を追加する。</summary>
    public void AppendCommand(nint menu, int id, string text, bool isEnabled)
        => Append(menu, MF_STRING | (isEnabled ? 0 : MF_GRAYED), (nuint)id, new Entry(text, IsSeparator: false, HasSubmenu: false));

    /// <summary>サブメニューを開く項目を追加する。</summary>
    public void AppendSubmenu(nint menu, nint submenu, string text, bool isEnabled)
        => Append(menu, MF_POPUP | (isEnabled ? 0 : MF_GRAYED), (nuint)submenu, new Entry(text, IsSeparator: false, HasSubmenu: true));

    public void AppendSeparator(nint menu)
        => Append(menu, MF_SEPARATOR, 0, new Entry("", IsSeparator: true, HasSubmenu: false));

    private void Append(nint menu, uint flags, nuint idOrSubmenu, Entry entry)
    {
        _entries.Add(entry);
        AppendOwnerDrawMenu(menu, flags | MF_OWNERDRAW, idOrSubmenu, _entries.Count);
    }

    /// <summary>メニュー全体（サブメニューを含む）の背景を合わせ、チェックマーク用の左の余白を無くす。</summary>
    /// <remarks>サブメニューにも反映させるため、項目をすべて追加したあとに呼ぶ。</remarks>
    public void ApplyTo(nint menu)
    {
        var info = new MENUINFO
        {
            cbSize = (uint)sizeof(MENUINFO),
            fMask = MIM_BACKGROUND | MIM_STYLE | MIM_APPLYTOSUBMENUS,
            dwStyle = MNS_NOCHECK,
            hbrBack = _backgroundBrush,
        };
        SetMenuInfo(menu, &info);
    }

    #endregion

    #region 計測・描画（WM_MEASUREITEM / WM_DRAWITEM）

    public void Measure(MEASUREITEMSTRUCT* item)
    {
        if (GetEntry(item->itemData) is not { } entry) return;

        if (entry.IsSeparator)
        {
            item->itemWidth = 0;
            item->itemHeight = (uint)Px(SeparatorHeight);
            return;
        }

        var width = Px(PaddingLeft) + MeasureText(entry.Text).Width + Px(entry.HasSubmenu ? ArrowArea : PaddingRight);
        item->itemWidth = (uint)Math.Max(width, Px(MinWidth));
        item->itemHeight = (uint)(_lineHeight + Px(PaddingVertical) * 2);
    }

    public void Draw(DRAWITEMSTRUCT* item)
    {
        if (GetEntry(item->itemData) is not { } entry) return;

        var hdc = item->hDC;
        var bounds = item->rcItem;
        FillRect(hdc, &bounds, _backgroundBrush);

        if (entry.IsSeparator)
        {
            // 線は文字の書き出し位置から右端の手前まで
            var top = (bounds.top + bounds.bottom) / 2;
            var line = new RECT { left = bounds.left + Px(PaddingLeft), top = top, right = bounds.right - Px(SeparatorInset), bottom = top + Math.Max(1, Px(1)) };
            FillRect(hdc, &line, _separatorBrush);
            return;
        }

        var isDisabled = (item->itemState & (ODS_GRAYED | ODS_DISABLED)) != 0;
        if (!isDisabled && (item->itemState & ODS_SELECTED) != 0)
        {
            var oldBrush = SelectObject(hdc, _hoverBrush);
            var oldPen = SelectObject(hdc, GetStockObject(NULL_PEN));
            RoundRect(hdc, bounds.left + Px(HoverInsetX), bounds.top + Px(HoverInsetY), bounds.right - Px(HoverInsetX), bounds.bottom - Px(HoverInsetY), Px(HoverRadius), Px(HoverRadius));
            SelectObject(hdc, oldPen);
            SelectObject(hdc, oldBrush);
        }

        SetBkMode(hdc, TRANSPARENT);
        SetTextColor(hdc, isDisabled ? _palette.DisabledText : _palette.Text);
        var oldFont = SelectObject(hdc, _font);

        var textBounds = bounds;
        textBounds.left += Px(PaddingLeft);
        textBounds.right -= Px(entry.HasSubmenu ? ArrowArea : PaddingRight);
        // 「&」をアクセスキーの印として扱わない（名前をそのまま出す）
        DrawText(hdc, entry.Text, -1, &textBounds, DT_SINGLELINE | DT_VCENTER | DT_NOPREFIX | DT_END_ELLIPSIS);

        if (entry.HasSubmenu && _iconFont != 0)
        {
            SelectObject(hdc, _iconFont);
            var arrowBounds = bounds;
            arrowBounds.left = bounds.right - Px(ArrowArea);
            arrowBounds.right = bounds.right - Px(HoverInsetX);
            DrawText(hdc, ChevronGlyph, -1, &arrowBounds, DT_SINGLELINE | DT_VCENTER | DT_CENTER | DT_NOPREFIX);

            // Windows が後から描く標準の矢印を止める（テーマによっては背景と同じ色になって見えない）
            ExcludeClipRect(hdc, bounds.left, bounds.top, bounds.right, bounds.bottom);
        }

        SelectObject(hdc, oldFont);
    }

    private Entry? GetEntry(nuint itemData)
    {
        var index = (int)itemData - 1;
        return index >= 0 && index < _entries.Count ? _entries[index] : null;
    }

    #endregion

    #region フォント・寸法

    private int Px(double value) => (int)Math.Round(value * _scale);

    private (int Width, int Height) MeasureText(string text)
    {
        var hdc = GetDC(0);
        try
        {
            var oldFont = SelectObject(hdc, _font);
            var bounds = new RECT();
            DrawText(hdc, text, -1, &bounds, DT_SINGLELINE | DT_NOPREFIX | DT_CALCRECT);
            SelectObject(hdc, oldFont);
            return (bounds.right - bounds.left, bounds.bottom - bounds.top);
        }
        finally
        {
            ReleaseDC(0, hdc);
        }
    }

    private static nint CreateFont(string face, int height)
    {
        var logFont = new LOGFONTW
        {
            lfHeight = height,
            lfWeight = 400,
            lfCharSet = DEFAULT_CHARSET,
            lfQuality = CLEARTYPE_QUALITY,
        };
        CopyToFixed(face, logFont.lfFaceName, 32);
        return CreateFontIndirect(&logFont);
    }

    /// <summary>候補のうち、この PC に入っている最初のフォント名。</summary>
    /// <remarks>無い名前を指定しても GDI は別のフォントで代用して作れてしまうため、先に有無を調べる。</remarks>
    private static string? FindInstalledFont(string[] faces)
    {
        var hdc = GetDC(0);
        try
        {
            foreach (var face in faces)
            {
                var logFont = new LOGFONTW { lfCharSet = DEFAULT_CHARSET };
                CopyToFixed(face, logFont.lfFaceName, 32);
                var found = 0;
                EnumFontFamiliesEx(hdc, &logFont, &OnFontFound, (nint)(&found), 0);
                if (found != 0)
                {
                    return face;
                }
            }
            return null;
        }
        finally
        {
            ReleaseDC(0, hdc);
        }
    }

    [UnmanagedCallersOnly]
    private static int OnFontFound(void* logFont, void* textMetric, uint fontType, nint found)
    {
        *(int*)found = 1;
        // 1 件見つかれば十分なので列挙を止める
        return 0;
    }

    #endregion

    #region 配色

    /// <summary>アプリのダークモード設定（Windows の「アプリ モード」）。</summary>
    /// <remarks>メニューの枠は uxtheme がこの設定に従うので、中身も同じ設定に合わせる。読めなければライト。</remarks>
    private static bool IsDarkMode()
    {
        try
        {
            return Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", 1) is 0;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or IOException)
        {
            return false;
        }
    }

    /// <summary>色（COLORREF。0x00BBGGRR）。</summary>
    private sealed record Palette(uint Background, uint Hover, uint Separator, uint Text, uint DisabledText)
    {
        public static readonly Palette Dark = new(Rgb(0x2C, 0x2C, 0x2C), Rgb(0x3D, 0x3D, 0x3D), Rgb(0x48, 0x48, 0x48), Rgb(0xFF, 0xFF, 0xFF), Rgb(0x7A, 0x7A, 0x7A));

        public static readonly Palette Light = new(Rgb(0xF9, 0xF9, 0xF9), Rgb(0xE6, 0xE6, 0xE6), Rgb(0xDC, 0xDC, 0xDC), Rgb(0x1A, 0x1A, 0x1A), Rgb(0xA0, 0xA0, 0xA0));

        private static uint Rgb(byte r, byte g, byte b) => (uint)(r | (g << 8) | (b << 16));
    }

    #endregion

    private sealed record Entry(string Text, bool IsSeparator, bool HasSubmenu);

    /// <remarks>メニューが背景のブラシを使っているので、メニューを破棄してから呼ぶ。</remarks>
    public void Dispose()
    {
        DeleteObject(_font);
        if (_iconFont != 0) DeleteObject(_iconFont);
        DeleteObject(_backgroundBrush);
        DeleteObject(_hoverBrush);
        DeleteObject(_separatorBrush);
    }
}
