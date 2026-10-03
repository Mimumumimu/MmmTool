using System.Runtime.InteropServices;

namespace MmmTool.Interop;

internal static partial class NativeMethods
{
    /// <summary>文字の項目</summary>
    public const uint MF_STRING = 0x0000;
    /// <summary>無効（灰色）の項目</summary>
    public const uint MF_GRAYED = 0x0001;
    /// <summary>サブメニューを開く項目</summary>
    public const uint MF_POPUP = 0x0010;
    /// <summary>オーナードローの項目</summary>
    public const uint MF_OWNERDRAW = 0x0100;
    /// <summary>区切り線</summary>
    public const uint MF_SEPARATOR = 0x0800;
    /// <summary>メニューの右端を指定位置に合わせる</summary>
    public const uint TPM_RIGHTALIGN = 0x0008;
    /// <summary>メニューの下端を指定位置に合わせる</summary>
    public const uint TPM_BOTTOMALIGN = 0x0020;
    /// <summary>右ボタンでも選べる</summary>
    public const uint TPM_RIGHTBUTTON = 0x0002;
    /// <summary>メニュー選択の通知を送らない</summary>
    public const uint TPM_NONOTIFY = 0x0080;
    /// <summary>選ばれた項目の ID を戻り値で返す</summary>
    public const uint TPM_RETURNCMD = 0x0100;
    /// <summary>メニューの開く向き（システムメトリック）</summary>
    public const int SM_MENUDROPALIGNMENT = 40;

    /// <summary>ポップアップメニューを作成する</summary>
    /// <returns>メニューのハンドル。失敗すれば 0</returns>
    [LibraryImport("user32.dll", SetLastError = true)]
    public static partial nint CreatePopupMenu();

    /// <summary>オーナードローの項目を追加する</summary>
    /// <param name="hMenu">追加先のメニューのハンドル</param>
    /// <param name="uFlags">項目の種類を示すフラグ（<c>MF_*</c>）</param>
    /// <param name="uIDNewItem">コマンド ID、またはサブメニューのハンドル</param>
    /// <param name="itemData">描画時に受け取る値</param>
    /// <returns>成功すれば true</returns>
    /// <remarks>lpNewItem の代わりに、描画時に受け取る値を渡す。</remarks>
    [LibraryImport("user32.dll", EntryPoint = "AppendMenuW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool AppendOwnerDrawMenu(nint hMenu, uint uFlags, nuint uIDNewItem, nint itemData);

    /// <summary>メニューを破棄する</summary>
    /// <param name="hMenu">メニューのハンドル</param>
    /// <returns>成功すれば true</returns>
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DestroyMenu(nint hMenu);

    /// <summary>ポップアップメニューを表示する</summary>
    /// <param name="hMenu">表示するメニューのハンドル</param>
    /// <param name="uFlags">表示方法のフラグ（<c>TPM_*</c>）</param>
    /// <param name="x">表示位置の X（画面座標）</param>
    /// <param name="y">表示位置の Y（画面座標）</param>
    /// <param name="hwnd">メッセージを受けるウィンドウのハンドル</param>
    /// <param name="lptpm">除外領域の情報（使わなければ 0）</param>
    /// <returns>選ばれた項目の ID。選ばれなければ 0</returns>
    [LibraryImport("user32.dll")]
    public static partial int TrackPopupMenuEx(nint hMenu, uint uFlags, int x, int y, nint hwnd, nint lptpm);

    /// <summary>システムメトリックを取得する</summary>
    /// <param name="nIndex">取得する項目（<c>SM_*</c>）</param>
    /// <returns>取得した値</returns>
    [LibraryImport("user32.dll")]
    public static partial int GetSystemMetrics(int nIndex);

    // Win32 のメニューは既定では常にライトの見た目になる。uxtheme の非公開 API（序数指定）でシステムのダーク設定に従わせる。
    // Windows 10 1903 以降で使える（エクスプローラー等も使っている）。見つからない環境では何もしない（ライトのまま）

    /// <summary>uxtheme の SetPreferredAppMode の序数</summary>
    private const int UxThemeOrdinalSetPreferredAppMode = 135;
    /// <summary>uxtheme の FlushMenuThemes の序数</summary>
    private const int UxThemeOrdinalFlushMenuThemes = 136;
    /// <summary>システムのダーク設定に従う（AllowDark）</summary>
    private const int PreferredAppModeAllowDark = 1;

    /// <summary>DLL を読み込む</summary>
    /// <param name="lpLibFileName">DLL のファイル名</param>
    /// <returns>モジュールのハンドル。失敗すれば 0</returns>
    [LibraryImport("kernel32.dll", EntryPoint = "LoadLibraryW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial nint LoadLibrary(string lpLibFileName);

    /// <summary>DLL の関数のアドレスを取得する</summary>
    /// <param name="hModule">モジュールのハンドル</param>
    /// <param name="lpProcName">関数の序数（下位ワードに指定）</param>
    /// <returns>関数のアドレス。見つからなければ 0</returns>
    [LibraryImport("kernel32.dll")]
    private static partial nint GetProcAddress(nint hModule, nint lpProcName);

    /// <summary>メニューをシステムのダーク／ライト設定に従わせる。</summary>
    public static unsafe void AllowDarkMenus()
    {
        var uxtheme = LoadLibrary("uxtheme.dll");
        if (uxtheme == 0) return;

        var setPreferredAppMode = (delegate* unmanaged<int, int>)GetProcAddress(uxtheme, UxThemeOrdinalSetPreferredAppMode);
        if (setPreferredAppMode is not null)
        {
            setPreferredAppMode(PreferredAppModeAllowDark);
        }
        FlushMenuThemes();
    }

    /// <summary>メニューの見た目をいまのテーマで作り直させる</summary>
    /// <remarks>ダーク／ライトの切り替え時に使う。</remarks>
    public static unsafe void FlushMenuThemes()
    {
        var uxtheme = LoadLibrary("uxtheme.dll");
        if (uxtheme == 0) return;

        var flushMenuThemes = (delegate* unmanaged<void>)GetProcAddress(uxtheme, UxThemeOrdinalFlushMenuThemes);
        if (flushMenuThemes is not null)
        {
            flushMenuThemes();
        }
    }
}
