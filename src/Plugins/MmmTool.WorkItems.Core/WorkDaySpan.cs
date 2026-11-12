namespace MmmTool.WorkItems.Core;

/// <summary>実績の合計から、9:00 に始業したときの終了時刻を求めて、「9:00 ～ 21:30」の形にする</summary>
/// <remarks>
/// 昼休みは 12:00 から 60 分。
/// 終了が 12:00 を超えるときだけ、昼休みの 60 分を足す。
/// ちょうど 12:00 までなら足さない。
/// 始業と昼休みは固定で、設定にしない。
/// ユーザーの決定。
/// </remarks>
public static class WorkDaySpan
{
    /// <summary>始業の時刻</summary>
    private const int StartMinutes = 9 * 60;

    /// <summary>昼休みの始まり</summary>
    private const int LunchStartMinutes = 12 * 60;

    /// <summary>昼休みの長さ</summary>
    /// <remarks>単位は分</remarks>
    private const int LunchMinutes = 60;

    /// <summary>時間帯を作る</summary>
    /// <param name="totalHours">実績の合計 (時間)</param>
    /// <returns>
    /// 「9:00 ～ 21:30」の形
    /// 合計は分に丸める
    /// 終了が 24 時を超えるときは 25:30 のように 24 以上の時で出す
    /// 合計が 0 以下なら空文字
    /// </returns>
    public static string Format(double totalHours)
    {
        if (totalHours <= 0)
        {
            return "";
        }

        var end = StartMinutes + (int)Math.Round(totalHours * 60, MidpointRounding.AwayFromZero);
        if (end > LunchStartMinutes)
        {
            end += LunchMinutes;
        }
        return $"{StartMinutes / 60}:{StartMinutes % 60:00} ～ {end / 60}:{end % 60:00}";
    }
}
