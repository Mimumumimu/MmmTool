using MmmTool.CodeMaster.Core;

namespace MmmTool.WorkItems.Core;

/// <summary>警告を確かめるきっかけ (利用者が確定した入力)</summary>
public enum WorkWarningTrigger
{
    /// <summary>状態を選んだ</summary>
    Status,

    /// <summary>進捗度を選んだ</summary>
    Progress,

    /// <summary>実績を入れた (0 より大きい値)</summary>
    Actual,

    /// <summary>備考 (日)を入れた</summary>
    DayNote,
}

/// <summary>作業の状態と、進捗度・実績の食い違いの警告</summary>
/// <remarks>
/// 状態と進捗度は連動させない (入力は止めない)。状態・進捗度・実績を確定した直後に、そのきっかけで起きた食い違いだけを、画面が短く知らせる。
/// 前から食い違っている行や、直した値と関係のない食い違いは、知らせない。保存はしない。表示のたびに、作業の値から求める。
/// </remarks>
public static class WorkItemWarnings
{
    /// <summary>進捗度が 100% のときの値 (%)</summary>
    private const int CompletePercent = 100;

    /// <summary>完了なのに、進捗度が 100% ではないときの警告の文</summary>
    public const string ClosedButNotComplete = "進捗度が 100% ではありません。";

    /// <summary>未対応なのに、進捗度か実績が入っているときの警告の文</summary>
    public const string OpenButStarted = "状態が「未対応」です。";

    /// <summary>入力日が開始日より前のときの警告の文</summary>
    public const string BeforeStartDate = "入力日が開始日より前です。";

    /// <summary>入力したきっかけに合う、食い違いの警告の文を返す</summary>
    /// <param name="item">入力を保存したあとの作業 (実績の累計は、最新の値)</param>
    /// <param name="book">コードブック (進捗度のコード番号を、%の値にする)</param>
    /// <param name="trigger">確定した入力</param>
    /// <param name="inputDate">入力日</param>
    /// <returns>警告の文。知らせることが無ければ null</returns>
    /// <remarks>
    /// 状態を選んだときは、完了なのに進捗度が 100% ではない場合だけ。
    /// 進捗度を選んだとき・実績を入れたときは、未対応なのに値が入った場合だけ。
    /// 実績を入れたとき・備考 (日)を入れたときは、入力日が開始日より前の場合も知らせる (開始日が空なら知らせない)。
    /// 状態を未対応にしたときは知らせない (選んだ状態を言い直すだけになるため)。完了の行の進捗度を下げたときも、知らせない (合意した警告の対象外)。
    /// </remarks>
    public static string? Check(WorkItem item, CodeBook book, WorkWarningTrigger trigger, DateOnly inputDate)
    {
        var percent = WorkCodes.ProgressPercent(book, item.ProgressCodeNo);
        return trigger switch
        {
            WorkWarningTrigger.Status when item.Status == WorkItemStatus.Closed && percent != CompletePercent => ClosedButNotComplete,
            WorkWarningTrigger.Progress when item.Status == WorkItemStatus.Open && percent > 0 => OpenButStarted,
            WorkWarningTrigger.Actual when item.Status == WorkItemStatus.Open && item.ActualTotalHours > 0 => OpenButStarted,
            WorkWarningTrigger.Actual or WorkWarningTrigger.DayNote when item.StartDate is { } start && inputDate < start => BeforeStartDate,
            _ => null,
        };
    }
}
