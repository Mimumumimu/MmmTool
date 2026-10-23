namespace MmmTool.WorkItems.Core;

/// <summary>
/// 作業管理の変更を、日ごとの内容の変更として知らせる、作業報告の提供元。
/// </summary>
/// <remarks>
/// 作業管理の画面・スケジュールでの編集と、他の人の更新の取り込みを、ノートなど内容を出す側へ知らせる。
/// 内容を出す側は、変わったと知ったら、保存先から読み直す。保存先へ書いたあとにメモリを更新して通知するため、読み直した内容は新しい。
/// 提供元とサービスは、どちらもアプリ全体で 1 つなので、購読は解除しない。
/// サービスを持たない環境 (Web 版のサーバー)は、基底の <see cref="WorkReportDayContentSource"/> を登録する。
/// </remarks>
public sealed class LiveWorkReportDayContentSource : WorkReportDayContentSource
{
    /// <summary>提供元を作る</summary>
    /// <param name="repository">作業管理の保存先</param>
    /// <param name="service">作業管理の読み書き。変更の通知を受け取る</param>
    public LiveWorkReportDayContentSource(IWorkItemRepository repository, WorkItemService service)
        : base(repository)
    {
        service.Changed += (_, _) => RaiseChanged();
    }
}
