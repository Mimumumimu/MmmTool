namespace MmmTool.Interop;

/// <summary>Win32 API の宣言（P/Invoke）</summary>
/// <remarks>
/// 用途ごとに partial で分けている（<c>NativeMethods.&lt;用途&gt;.cs</c>）。
/// Ime（IME の切り替え）/ MessageBox / Window（前面表示・DPI）/ PseudoConsole（ConPTY・プロセス作成）。
/// トレイ・メニュー・擬似モーダルなど汎用の宣言は SDK（<c>MmmSdk.WinUI.Interop</c>、internal）にある。
/// </remarks>
internal static partial class NativeMethods;
