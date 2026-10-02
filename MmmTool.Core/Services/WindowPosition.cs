namespace MmmTool.Core.Services;

/// <summary>
/// ウィンドウの位置（画面全体の物理ピクセル座標での左上）。
/// </summary>
/// <param name="X">左端。</param>
/// <param name="Y">上端。</param>
public sealed record WindowPosition(int X, int Y);
