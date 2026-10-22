using MmmTool.DayContent.Core;

namespace MmmTool.WorkItems.Core;

/// <summary>
/// 作業報告 (実績と予定のテキスト)を、日ごとの内容として提供する。ノートの画面に出す。
/// </summary>
/// <param name="repository">作業管理の保存先</param>
/// <remarks>
/// 文は <see cref="WorkReportBuilder"/> が作る。作業報告の窓と同じ形で、期間の終わりが報告日、始まりが実績の開始日。
/// 選択肢のキーは、作業報告の窓が覚える設定のキーと同じにして、選択を共有する。
/// </remarks>
public sealed class WorkReportDayContentSource(IWorkItemRepository repository) : IDayContentSource
{
    /// <summary>「実績に時間を出す」の選択を残す設定のキー</summary>
    public const string ShowHoursKey = "WorkItems.Report.ShowHours";

    /// <summary>「進捗度表示」の選択を残す設定のキー</summary>
    public const string ShowProgressKey = "WorkItems.Report.ShowProgress";

    /// <inheritdoc />
    public string Key => "WorkReport";

    /// <inheritdoc />
    public string Title => "作業報告";

    /// <summary>作業管理の機能のキー</summary>
    /// <remarks>設定ファイルにも使う。<c>WorkItemsFeature.Key</c> は、この値を使う。</remarks>
    public const string WorkItemsFeatureKey = "WorkItems";

    /// <inheritdoc />
    public string? FeatureKey => WorkItemsFeatureKey;

    /// <inheritdoc />
    public IReadOnlyList<DayContentOption> Options { get; } =
    [
        new DayContentOption(ShowProgressKey, "進捗度表示", true),
        new DayContentOption(ShowHoursKey, "実績に時間を出す", false),
    ];

    /// <inheritdoc />
    public async Task<DateOnly> GetDefaultFromAsync(DateOnly to, CancellationToken cancellationToken = default)
        => await repository.GetLatestRecordedDateAsync(to, cancellationToken).ConfigureAwait(false) ?? to.AddDays(-1);

    /// <inheritdoc />
    public Task<IReadOnlyList<int>> GetPeopleAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
        => repository.GetReportPeopleAsync(from, to, cancellationToken);

    /// <inheritdoc />
    public async Task<string> GetTextAsync(DateOnly from, DateOnly to, int? userId, IReadOnlyDictionary<string, bool> options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        var data = await repository.LoadReportAsync(from, to, userId, cancellationToken).ConfigureAwait(false);
        return WorkReportBuilder.Build(
            data,
            from,
            to,
            options.GetValueOrDefault(ShowHoursKey, false),
            options.GetValueOrDefault(ShowProgressKey, true));
    }
}
