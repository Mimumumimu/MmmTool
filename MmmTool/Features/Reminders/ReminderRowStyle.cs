using Windows.UI.Text;

namespace MmmTool.Features.Reminders;

/// <summary>リマインダーの行・件名の見た目（完了・削除済みの表現）</summary>
/// <remarks>メイン画面と一覧画面で同じ表現にするため、XAML の <c>x:Bind</c> から関数として呼ぶ。</remarks>
public static class ReminderRowStyle
{
    /// <summary>完了・削除済みの行や件名は淡くする</summary>
    /// <param name="isDimmed">淡くするか（完了・削除済み）</param>
    /// <returns>不透明度</returns>
    public static double Opacity(bool isDimmed) => isDimmed ? 0.5 : 1.0;

    /// <summary>完了・削除済みの文字は取り消し線を引く</summary>
    /// <param name="isStruck">取り消し線を引くか（完了・削除済み）</param>
    /// <returns>文字の装飾</returns>
    public static TextDecorations Strike(bool isStruck) => isStruck ? TextDecorations.Strikethrough : TextDecorations.None;
}
