using System.Runtime.InteropServices;

namespace MmmTool.Interop;

internal static partial class NativeMethods
{
    /// <summary>アイコンを追加する</summary>
    public const uint NIM_ADD = 0x0;
    /// <summary>アイコンを変更する</summary>
    public const uint NIM_MODIFY = 0x1;
    /// <summary>アイコンを削除する</summary>
    public const uint NIM_DELETE = 0x2;
    /// <summary>通知のバージョンを設定する</summary>
    public const uint NIM_SETVERSION = 0x4;
    /// <summary>コールバックメッセージを有効にする</summary>
    public const uint NIF_MESSAGE = 0x01;
    /// <summary>アイコンを有効にする</summary>
    public const uint NIF_ICON = 0x02;
    /// <summary>ツールチップを有効にする</summary>
    public const uint NIF_TIP = 0x04;
    /// <summary>バルーン通知を有効にする</summary>
    public const uint NIF_INFO = 0x10;
    /// <summary>標準のツールチップを表示する</summary>
    public const uint NIF_SHOWTIP = 0x80;
    /// <summary>情報アイコン</summary>
    public const uint NIIF_INFO = 0x1;
    /// <summary>エラーアイコン</summary>
    public const uint NIIF_ERROR = 0x3;
    /// <summary>通知のバージョン 4</summary>
    public const uint NOTIFYICON_VERSION_4 = 4;
    /// <summary>アイコンが選択された（クリック）通知</summary>
    public const uint NIN_SELECT = 0x0400;
    /// <summary>アイコンがキーボードで選択された通知</summary>
    public const uint NIN_KEYSELECT = 0x0401;

    /// <summary>トレイアイコンの情報</summary>
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct NOTIFYICONDATAW
    {
        /// <summary>構造体のサイズ（バイト）</summary>
        public uint cbSize;
        /// <summary>ウィンドウのハンドル</summary>
        public nint hWnd;
        /// <summary>アイコンの識別番号</summary>
        public uint uID;
        /// <summary>有効にする項目のフラグ</summary>
        public uint uFlags;
        /// <summary>通知を受けるメッセージ番号</summary>
        public uint uCallbackMessage;
        /// <summary>アイコンのハンドル</summary>
        public nint hIcon;
        /// <summary>ツールチップの文字</summary>
        public fixed char szTip[128];
        /// <summary>アイコンの状態</summary>
        public uint dwState;
        /// <summary>有効にする状態のビット</summary>
        public uint dwStateMask;
        /// <summary>バルーン通知の本文</summary>
        public fixed char szInfo[256];
        /// <summary>バージョン</summary>
        /// <remarks>uTimeout と共用（NIM_SETVERSION のときはバージョン）。</remarks>
        public uint uVersion;
        /// <summary>バルーン通知のタイトル</summary>
        public fixed char szInfoTitle[64];
        /// <summary>バルーン通知のアイコンの種類</summary>
        public uint dwInfoFlags;
        /// <summary>アイコンの GUID</summary>
        public Guid guidItem;
        /// <summary>バルーン通知のアイコンのハンドル</summary>
        public nint hBalloonIcon;
    }

    /// <summary>トレイアイコンを追加・変更・削除する</summary>
    /// <param name="dwMessage">操作の種類（<c>NIM_*</c>）</param>
    /// <param name="lpData">トレイアイコンの情報</param>
    /// <returns>成功すれば true</returns>
    [LibraryImport("shell32.dll", EntryPoint = "Shell_NotifyIconW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static unsafe partial bool Shell_NotifyIcon(uint dwMessage, NOTIFYICONDATAW* lpData);

    /// <summary>固定長の文字列欄へ書き込む</summary>
    /// <param name="value">書き込む文字列</param>
    /// <param name="buffer">書き込み先の固定長バッファ</param>
    /// <param name="length">バッファの文字数（終端を含む）</param>
    /// <remarks>収まらない分は切り捨て、必ず終端する。</remarks>
    public static unsafe void CopyToFixed(string value, char* buffer, int length)
    {
        var count = Math.Min(value.Length, length - 1);
        value.AsSpan(0, count).CopyTo(new Span<char>(buffer, count));
        buffer[count] = '\0';
    }

    /// <summary>アイコンの画像</summary>
    public const uint IMAGE_ICON = 1;
    /// <summary>ファイルから読み込む</summary>
    public const uint LR_LOADFROMFILE = 0x10;
    /// <summary>小アイコンの幅（システムメトリック）</summary>
    public const int SM_CXSMICON = 49;
    /// <summary>標準のアプリケーションアイコン</summary>
    public const nint IDI_APPLICATION = 32512;

    /// <summary>画像（アイコン等）を読み込む</summary>
    /// <param name="hInst">リソースを持つインスタンスのハンドル（ファイルから読むときは 0）</param>
    /// <param name="name">ファイルのパスまたはリソース名</param>
    /// <param name="type">画像の種類（<c>IMAGE_*</c>）</param>
    /// <param name="cx">幅</param>
    /// <param name="cy">高さ</param>
    /// <param name="fuLoad">読み込み方法（<c>LR_*</c>）</param>
    /// <returns>画像のハンドル。失敗すれば 0</returns>
    [LibraryImport("user32.dll", EntryPoint = "LoadImageW", StringMarshalling = StringMarshalling.Utf16)]
    public static partial nint LoadImage(nint hInst, string name, uint type, int cx, int cy, uint fuLoad);

    /// <summary>アイコンを読み込む</summary>
    /// <param name="hInstance">リソースを持つインスタンスのハンドル（標準のアイコンなら 0）</param>
    /// <param name="lpIconName">アイコンの名前または標準アイコンの番号</param>
    /// <returns>アイコンのハンドル</returns>
    [LibraryImport("user32.dll", EntryPoint = "LoadIconW")]
    public static partial nint LoadIcon(nint hInstance, nint lpIconName);

    /// <summary>アイコンを破棄する</summary>
    /// <param name="hIcon">アイコンのハンドル</param>
    /// <returns>成功すれば true</returns>
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DestroyIcon(nint hIcon);

    /// <summary>DPI を指定してシステムメトリックを取得する</summary>
    /// <param name="nIndex">取得する項目（<c>SM_*</c>）</param>
    /// <param name="dpi">基準にする DPI</param>
    /// <returns>取得した値</returns>
    [LibraryImport("user32.dll")]
    public static partial int GetSystemMetricsForDpi(int nIndex, uint dpi);

    /// <summary>システムの DPI を取得する</summary>
    /// <returns>システムの DPI（100% で 96）</returns>
    [LibraryImport("user32.dll")]
    public static partial uint GetDpiForSystem();
}
