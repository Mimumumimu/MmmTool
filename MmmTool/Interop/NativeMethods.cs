namespace MmmTool.Interop;

/// <summary>Win32 API の宣言（P/Invoke）</summary>
/// <remarks>
/// 用途ごとに partial で分けている（<c>NativeMethods.&lt;用途&gt;.cs</c>）。
/// MessageBox / PseudoConsole（ConPTY・プロセス作成）。
/// トレイ・メニュー・擬似モーダル・前面表示・DPI など汎用の処理は SDK（<c>MmmSdk.WinUI</c>）にある（前面表示・DPI は <c>Window</c> の拡張メソッド）。
/// </remarks>
internal static partial class NativeMethods;
