using System.Runtime.InteropServices;

namespace MmmTool.Interop;

internal static partial class NativeMethods
{
    /// <summary>メニュー項目のオーナードロー</summary>
    public const uint ODT_MENU = 1;
    /// <summary>項目が選択されている</summary>
    public const uint ODS_SELECTED = 0x0001;
    /// <summary>項目が無効（灰色）</summary>
    public const uint ODS_GRAYED = 0x0002;
    /// <summary>項目が無効</summary>
    public const uint ODS_DISABLED = 0x0004;
    /// <summary>背景のブラシを設定する</summary>
    public const uint MIM_BACKGROUND = 0x00000002;
    /// <summary>スタイルを設定する</summary>
    public const uint MIM_STYLE = 0x00000010;
    /// <summary>サブメニューにも適用する</summary>
    public const uint MIM_APPLYTOSUBMENUS = 0x80000000;
    /// <summary>チェックマーク用の左の余白を無くす</summary>
    public const uint MNS_NOCHECK = 0x80000000;
    /// <summary>水平方向の中央に揃える</summary>
    public const uint DT_CENTER = 0x0001;
    /// <summary>垂直方向の中央に揃える</summary>
    public const uint DT_VCENTER = 0x0004;
    /// <summary>1 行で描く</summary>
    public const uint DT_SINGLELINE = 0x0020;
    /// <summary>描かずに大きさだけ計算する</summary>
    public const uint DT_CALCRECT = 0x0400;
    /// <summary>&amp; を下線の指定として扱わない</summary>
    public const uint DT_NOPREFIX = 0x0800;
    /// <summary>収まらない末尾を省略記号にする</summary>
    public const uint DT_END_ELLIPSIS = 0x8000;
    /// <summary>背景を塗りつぶさない</summary>
    public const int TRANSPARENT = 1;
    /// <summary>何も描かないペン</summary>
    public const int NULL_PEN = 8;
    /// <summary>既定の文字セット</summary>
    public const byte DEFAULT_CHARSET = 1;
    /// <summary>ClearType で描く品質</summary>
    public const byte CLEARTYPE_QUALITY = 5;
    /// <summary>最も近いモニターを返す</summary>
    public const uint MONITOR_DEFAULTTONEAREST = 2;
    /// <summary>実効 DPI</summary>
    public const int MDT_EFFECTIVE_DPI = 0;

    /// <summary>四角形</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        /// <summary>左端</summary>
        public int left;
        /// <summary>上端</summary>
        public int top;
        /// <summary>右端</summary>
        public int right;
        /// <summary>下端</summary>
        public int bottom;
    }

    /// <summary>座標</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        /// <summary>X 座標</summary>
        public int x;
        /// <summary>Y 座標</summary>
        public int y;
    }

    /// <summary>オーナードロー項目の大きさを求める情報</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct MEASUREITEMSTRUCT
    {
        /// <summary>コントロールの種類</summary>
        public uint CtlType;
        /// <summary>コントロールの ID</summary>
        public uint CtlID;
        /// <summary>項目の ID</summary>
        public uint itemID;
        /// <summary>項目の幅</summary>
        public uint itemWidth;
        /// <summary>項目の高さ</summary>
        public uint itemHeight;
        /// <summary>項目に持たせた値</summary>
        public nuint itemData;
    }

    /// <summary>オーナードロー項目を描く情報</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct DRAWITEMSTRUCT
    {
        /// <summary>コントロールの種類</summary>
        public uint CtlType;
        /// <summary>コントロールの ID</summary>
        public uint CtlID;
        /// <summary>項目の ID</summary>
        public uint itemID;
        /// <summary>描画の動作</summary>
        public uint itemAction;
        /// <summary>項目の状態</summary>
        public uint itemState;
        /// <summary>コントロールまたはメニューのハンドル</summary>
        public nint hwndItem;
        /// <summary>描画先のデバイスコンテキスト</summary>
        public nint hDC;
        /// <summary>項目の四角形</summary>
        public RECT rcItem;
        /// <summary>項目に持たせた値</summary>
        public nuint itemData;
    }

    /// <summary>メニューの情報</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct MENUINFO
    {
        /// <summary>構造体のサイズ（バイト）</summary>
        public uint cbSize;
        /// <summary>有効にする項目のフラグ</summary>
        public uint fMask;
        /// <summary>メニューのスタイル</summary>
        public uint dwStyle;
        /// <summary>メニューの最大の高さ</summary>
        public uint cyMax;
        /// <summary>背景のブラシ</summary>
        public nint hbrBack;
        /// <summary>コンテキストヘルプの ID</summary>
        public uint dwContextHelpID;
        /// <summary>メニューに持たせた値</summary>
        public nuint dwMenuData;
    }

    /// <summary>論理フォントの情報</summary>
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct LOGFONTW
    {
        /// <summary>文字の高さ</summary>
        public int lfHeight;
        /// <summary>文字の平均の幅</summary>
        public int lfWidth;
        /// <summary>行の角度</summary>
        public int lfEscapement;
        /// <summary>文字の角度</summary>
        public int lfOrientation;
        /// <summary>太さ</summary>
        public int lfWeight;
        /// <summary>斜体</summary>
        public byte lfItalic;
        /// <summary>下線</summary>
        public byte lfUnderline;
        /// <summary>取り消し線</summary>
        public byte lfStrikeOut;
        /// <summary>文字セット</summary>
        public byte lfCharSet;
        /// <summary>出力の精度</summary>
        public byte lfOutPrecision;
        /// <summary>クリップの精度</summary>
        public byte lfClipPrecision;
        /// <summary>出力の品質</summary>
        public byte lfQuality;
        /// <summary>ピッチとファミリー</summary>
        public byte lfPitchAndFamily;
        /// <summary>フォント名</summary>
        public fixed char lfFaceName[32];
    }

    /// <summary>メニューの情報を設定する</summary>
    /// <param name="hMenu">メニューのハンドル</param>
    /// <param name="lpcmi">設定するメニューの情報</param>
    /// <returns>成功すれば true</returns>
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static unsafe partial bool SetMenuInfo(nint hMenu, MENUINFO* lpcmi);

    /// <summary>デバイスコンテキストを取得する</summary>
    /// <param name="hWnd">ウィンドウのハンドル（画面全体なら 0）</param>
    /// <returns>デバイスコンテキストのハンドル</returns>
    [LibraryImport("user32.dll")]
    public static partial nint GetDC(nint hWnd);

    /// <summary>デバイスコンテキストを解放する</summary>
    /// <param name="hWnd">ウィンドウのハンドル</param>
    /// <param name="hDC">解放するデバイスコンテキストのハンドル</param>
    /// <returns>解放できれば 1</returns>
    [LibraryImport("user32.dll")]
    public static partial int ReleaseDC(nint hWnd, nint hDC);

    /// <summary>四角形を塗りつぶす</summary>
    /// <param name="hDC">デバイスコンテキストのハンドル</param>
    /// <param name="lprc">塗りつぶす四角形</param>
    /// <param name="hbr">ブラシのハンドル</param>
    /// <returns>成功すれば 0 以外</returns>
    [LibraryImport("user32.dll")]
    public static unsafe partial int FillRect(nint hDC, RECT* lprc, nint hbr);

    /// <summary>文字を描く</summary>
    /// <param name="hdc">デバイスコンテキストのハンドル</param>
    /// <param name="lpchText">描く文字列</param>
    /// <param name="cchText">文字数（-1 で終端まで）</param>
    /// <param name="lprc">描く範囲の四角形</param>
    /// <param name="format">描き方のフラグ（<c>DT_*</c>）</param>
    /// <returns>描いた文字の高さ</returns>
    [LibraryImport("user32.dll", EntryPoint = "DrawTextW", StringMarshalling = StringMarshalling.Utf16)]
    public static unsafe partial int DrawText(nint hdc, string lpchText, int cchText, RECT* lprc, uint format);

    /// <summary>座標があるモニターを取得する</summary>
    /// <param name="pt">座標（画面座標）</param>
    /// <param name="dwFlags">モニターが無いときの扱い（<c>MONITOR_*</c>）</param>
    /// <returns>モニターのハンドル</returns>
    [LibraryImport("user32.dll")]
    public static partial nint MonitorFromPoint(POINT pt, uint dwFlags);

    /// <summary>モニターの DPI を取得する</summary>
    /// <param name="hmonitor">モニターのハンドル</param>
    /// <param name="dpiType">DPI の種類（<c>MDT_*</c>）</param>
    /// <param name="dpiX">水平方向の DPI を受け取る</param>
    /// <param name="dpiY">垂直方向の DPI を受け取る</param>
    /// <returns>成功すれば 0</returns>
    [LibraryImport("shcore.dll")]
    public static partial int GetDpiForMonitor(nint hmonitor, int dpiType, out uint dpiX, out uint dpiY);

    /// <summary>論理フォントからフォントを作成する</summary>
    /// <param name="lplf">フォントの情報</param>
    /// <returns>フォントのハンドル</returns>
    [LibraryImport("gdi32.dll", EntryPoint = "CreateFontIndirectW")]
    public static unsafe partial nint CreateFontIndirect(LOGFONTW* lplf);

    /// <summary>フォントを列挙する</summary>
    /// <param name="hdc">デバイスコンテキストのハンドル</param>
    /// <param name="lpLogfont">列挙する条件のフォント情報</param>
    /// <param name="lpProc">見つかるたびに呼ぶコールバック</param>
    /// <param name="lParam">コールバックへ渡す値</param>
    /// <param name="dwFlags">予約（0）</param>
    /// <returns>コールバックが最後に返した値</returns>
    [LibraryImport("gdi32.dll", EntryPoint = "EnumFontFamiliesExW")]
    public static unsafe partial int EnumFontFamiliesEx(
        nint hdc, LOGFONTW* lpLogfont, delegate* unmanaged<void*, void*, uint, nint, int> lpProc, nint lParam, uint dwFlags);

    /// <summary>描画オブジェクトを選択する</summary>
    /// <param name="hdc">デバイスコンテキストのハンドル</param>
    /// <param name="h">選択する描画オブジェクトのハンドル</param>
    /// <returns>直前に選択されていたオブジェクトのハンドル</returns>
    [LibraryImport("gdi32.dll")]
    public static partial nint SelectObject(nint hdc, nint h);

    /// <summary>描画オブジェクトを削除する</summary>
    /// <param name="ho">描画オブジェクトのハンドル</param>
    /// <returns>成功すれば true</returns>
    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DeleteObject(nint ho);

    /// <summary>標準の描画オブジェクトを取得する</summary>
    /// <param name="i">標準オブジェクトの種類（<c>NULL_PEN</c> など）</param>
    /// <returns>描画オブジェクトのハンドル</returns>
    [LibraryImport("gdi32.dll")]
    public static partial nint GetStockObject(int i);

    /// <summary>単色のブラシを作成する</summary>
    /// <param name="color">色（COLORREF）</param>
    /// <returns>ブラシのハンドル</returns>
    [LibraryImport("gdi32.dll")]
    public static partial nint CreateSolidBrush(uint color);

    /// <summary>文字の色を設定する</summary>
    /// <param name="hdc">デバイスコンテキストのハンドル</param>
    /// <param name="color">文字の色（COLORREF）</param>
    /// <returns>直前の文字の色</returns>
    [LibraryImport("gdi32.dll")]
    public static partial uint SetTextColor(nint hdc, uint color);

    /// <summary>背景の描き方を設定する</summary>
    /// <param name="hdc">デバイスコンテキストのハンドル</param>
    /// <param name="mode">背景の描き方（<c>TRANSPARENT</c> など）</param>
    /// <returns>直前の描き方</returns>
    [LibraryImport("gdi32.dll")]
    public static partial int SetBkMode(nint hdc, int mode);

    /// <summary>角丸の四角形を描く</summary>
    /// <param name="hdc">デバイスコンテキストのハンドル</param>
    /// <param name="left">左端</param>
    /// <param name="top">上端</param>
    /// <param name="right">右端</param>
    /// <param name="bottom">下端</param>
    /// <param name="width">角の丸みの幅</param>
    /// <param name="height">角の丸みの高さ</param>
    /// <returns>成功すれば true</returns>
    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool RoundRect(nint hdc, int left, int top, int right, int bottom, int width, int height);

    /// <summary>クリップ領域から四角形を除く</summary>
    /// <param name="hdc">デバイスコンテキストのハンドル</param>
    /// <param name="left">除く四角形の左端</param>
    /// <param name="top">除く四角形の上端</param>
    /// <param name="right">除く四角形の右端</param>
    /// <param name="bottom">除く四角形の下端</param>
    /// <returns>クリップ領域の種類</returns>
    [LibraryImport("gdi32.dll")]
    public static partial int ExcludeClipRect(nint hdc, int left, int top, int right, int bottom);
}
