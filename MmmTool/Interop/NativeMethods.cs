namespace MmmTool.Interop;

/// <summary>Win32 API の宣言（P/Invoke）</summary>
/// <remarks>
/// 用途ごとに partial で分けている（<c>NativeMethods.&lt;用途&gt;.cs</c>）。
/// Ime（IME の切り替え）/ MessageBox / Window（ウィンドウ・オーナー・DPI）/ Tray（タスクトレイ・アイコン）/
/// Menu（ポップアップメニュー・ダークモード）/ Gdi（メニューのオーナードロー）/ PseudoConsole（ConPTY・プロセス作成）。
/// </remarks>
internal static partial class NativeMethods;
