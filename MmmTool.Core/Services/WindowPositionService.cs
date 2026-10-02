using System.Drawing;
using MmmTool.Core.Repositories;
using MmmTool.Core.Repositories.Json;

namespace MmmTool.Core.Services;

/// <summary>
/// ウィンドウの位置をキー単位で保存・復元する。
/// </summary>
/// <remarks>保存は汎用設定ストア（<see cref="ISettingsStore"/>）に <c>WindowPosition.&lt;キー&gt;</c> として持つ。</remarks>
public sealed class WindowPositionService(ISettingsStore store)
{
    /// <summary>キーの接頭辞</summary>
    private const string KeyPrefix = "WindowPosition.";

    /// <summary>保存した位置を取得する</summary>
    /// <remarks>保存がなければ null。</remarks>
    public WindowPosition? Load(string key)
    {
        var storeKey = KeyPrefix + key;
        return store.Contains(storeKey)
            ? store.Get(storeKey, new WindowPosition(0, 0), CoreJsonContext.Readable.WindowPosition)
            : null;
    }

    /// <summary>位置を保存する。保存し終えるまで待つ。</summary>
    public Task SaveAsync(string key, WindowPosition position) =>
        store.SetAsync(KeyPrefix + key, position, CoreJsonContext.Readable.WindowPosition);

    /// <summary>ウィンドウが、いずれかの作業領域に十分に見えているか</summary>
    /// <remarks>全作業領域とウィンドウ矩形の重なる面積を合計し、ウィンドウ面積に対する割合が <paramref name="minRatio"/> 以上なら見えるとみなす。</remarks>
    /// <param name="window">ウィンドウの矩形（物理ピクセル）。</param>
    /// <param name="workAreas">全モニタの作業領域。</param>
    /// <param name="minRatio">見えるとみなす面積割合（0〜1）。</param>
    public static bool IsVisibleEnough(Rectangle window, IReadOnlyList<Rectangle> workAreas, double minRatio)
    {
        var windowArea = (long)window.Width * window.Height;
        if (windowArea <= 0) return false;

        long visible = 0;
        foreach (var area in workAreas)
        {
            var overlap = Rectangle.Intersect(window, area);
            visible += (long)overlap.Width * overlap.Height;
        }

        return (double)visible / windowArea >= minRatio;
    }
}
