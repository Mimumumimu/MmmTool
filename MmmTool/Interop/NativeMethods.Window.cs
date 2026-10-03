using System.Runtime.InteropServices;

namespace MmmTool.Interop;

internal static partial class NativeMethods
{
    /// <summary>何もしないメッセージ</summary>
    public const uint WM_NULL = 0x0000;
    /// <summary>ウィンドウが破棄されるメッセージ</summary>
    public const uint WM_DESTROY = 0x0002;
    /// <summary>システム設定が変更されたメッセージ</summary>
    public const uint WM_SETTINGCHANGE = 0x001A;
    /// <summary>オーナードロー項目の描画を求めるメッセージ</summary>
    public const uint WM_DRAWITEM = 0x002B;
    /// <summary>オーナードロー項目の大きさを求めるメッセージ</summary>
    public const uint WM_MEASUREITEM = 0x002C;
    /// <summary>コンテキストメニューを求めるメッセージ</summary>
    public const uint WM_CONTEXTMENU = 0x007B;
    /// <summary>アプリ独自のメッセージ番号の開始値</summary>
    public const uint WM_APP = 0x8000;

    /// <summary>ウィンドウクラスの情報</summary>
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct WNDCLASSEXW
    {
        /// <summary>構造体のサイズ（バイト）</summary>
        public uint cbSize;
        /// <summary>クラスのスタイル</summary>
        public uint style;
        /// <summary>ウィンドウプロシージャ</summary>
        public delegate* unmanaged<nint, uint, nint, nint, nint> lpfnWndProc;
        /// <summary>クラスに追加で確保するバイト数</summary>
        public int cbClsExtra;
        /// <summary>ウィンドウに追加で確保するバイト数</summary>
        public int cbWndExtra;
        /// <summary>インスタンスのハンドル</summary>
        public nint hInstance;
        /// <summary>アイコンのハンドル</summary>
        public nint hIcon;
        /// <summary>カーソルのハンドル</summary>
        public nint hCursor;
        /// <summary>背景のブラシ</summary>
        public nint hbrBackground;
        /// <summary>メニューの名前</summary>
        public char* lpszMenuName;
        /// <summary>クラス名</summary>
        public char* lpszClassName;
        /// <summary>小アイコンのハンドル</summary>
        public nint hIconSm;
    }

    /// <summary>モジュールのハンドルを取得する</summary>
    /// <param name="lpModuleName">モジュール名。null なら実行中の EXE</param>
    /// <returns>モジュールのハンドル</returns>
    [LibraryImport("kernel32.dll", EntryPoint = "GetModuleHandleW", StringMarshalling = StringMarshalling.Utf16)]
    public static partial nint GetModuleHandle(string? lpModuleName);

    /// <summary>ウィンドウクラスを登録する</summary>
    /// <param name="lpwcx">登録するウィンドウクラスの情報</param>
    /// <returns>登録したクラスのアトム。失敗すれば 0</returns>
    [LibraryImport("user32.dll", EntryPoint = "RegisterClassExW", SetLastError = true)]
    public static unsafe partial ushort RegisterClassEx(WNDCLASSEXW* lpwcx);

    /// <summary>ウィンドウクラスの登録を解除する</summary>
    /// <param name="lpClassName">クラス名</param>
    /// <param name="hInstance">クラスを登録したインスタンスのハンドル</param>
    /// <returns>成功すれば true</returns>
    [LibraryImport("user32.dll", EntryPoint = "UnregisterClassW", StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool UnregisterClass(string lpClassName, nint hInstance);

    /// <summary>ウィンドウを作成する</summary>
    /// <param name="dwExStyle">拡張ウィンドウスタイル</param>
    /// <param name="lpClassName">クラス名</param>
    /// <param name="lpWindowName">ウィンドウ名</param>
    /// <param name="dwStyle">ウィンドウスタイル</param>
    /// <param name="x">左端の位置</param>
    /// <param name="y">上端の位置</param>
    /// <param name="nWidth">幅</param>
    /// <param name="nHeight">高さ</param>
    /// <param name="hWndParent">親ウィンドウのハンドル</param>
    /// <param name="hMenu">メニューのハンドル</param>
    /// <param name="hInstance">インスタンスのハンドル</param>
    /// <param name="lpParam">作成時に渡す値</param>
    /// <returns>作成したウィンドウのハンドル。失敗すれば 0</returns>
    [LibraryImport("user32.dll", EntryPoint = "CreateWindowExW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    public static partial nint CreateWindowEx(
        uint dwExStyle, string lpClassName, string lpWindowName, uint dwStyle,
        int x, int y, int nWidth, int nHeight, nint hWndParent, nint hMenu, nint hInstance, nint lpParam);

    /// <summary>ウィンドウを破棄する</summary>
    /// <param name="hWnd">ウィンドウのハンドル</param>
    /// <returns>成功すれば true</returns>
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DestroyWindow(nint hWnd);

    /// <summary>既定のウィンドウプロシージャ</summary>
    /// <param name="hWnd">ウィンドウのハンドル</param>
    /// <param name="msg">メッセージ</param>
    /// <param name="wParam">メッセージの付加情報（wParam）</param>
    /// <param name="lParam">メッセージの付加情報（lParam）</param>
    /// <returns>メッセージの処理結果</returns>
    [LibraryImport("user32.dll", EntryPoint = "DefWindowProcW")]
    public static partial nint DefWindowProc(nint hWnd, uint msg, nint wParam, nint lParam);

    /// <summary>メッセージをキューに入れる</summary>
    /// <param name="hWnd">送り先ウィンドウのハンドル</param>
    /// <param name="msg">メッセージ</param>
    /// <param name="wParam">メッセージの付加情報（wParam）</param>
    /// <param name="lParam">メッセージの付加情報（lParam）</param>
    /// <returns>成功すれば true</returns>
    [LibraryImport("user32.dll", EntryPoint = "PostMessageW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool PostMessage(nint hWnd, uint msg, nint wParam, nint lParam);

    /// <summary>システム全体で一意のメッセージ番号を登録する</summary>
    /// <param name="lpString">メッセージの名前</param>
    /// <returns>登録したメッセージ番号</returns>
    [LibraryImport("user32.dll", EntryPoint = "RegisterWindowMessageW", StringMarshalling = StringMarshalling.Utf16)]
    public static partial uint RegisterWindowMessage(string lpString);

    /// <summary>ウィンドウを前面に出す</summary>
    /// <param name="hWnd">ウィンドウのハンドル</param>
    /// <returns>前面に出せれば true</returns>
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetForegroundWindow(nint hWnd);

    /// <summary>ウィンドウの DPI（ウィンドウがあるモニターの DPI）</summary>
    /// <param name="hWnd">ウィンドウのハンドル</param>
    /// <returns>DPI（100% で 96）</returns>
    /// <remarks>表示する前に大きさを決めるために使う（XamlRoot は表示するまで無いため）。96 が 100%。</remarks>
    [LibraryImport("user32.dll")]
    public static partial uint GetDpiForWindow(nint hWnd);
}
