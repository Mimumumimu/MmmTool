using MmmSdk.Core.Components.Storage;

namespace MmmTool.WorkItems.Core.Json;

/// <summary>作業リストを JSON ファイルに保存する</summary>
/// <param name="store">JSON ファイルの読み書き</param>
/// <remarks>
/// 行は <c>WorkItems.json</c>、日ごとの記録は <c>WorkRecords.json</c>、進捗度のラベルは <c>WorkProgressLevels.json</c> に分けて保存する。
/// 操作のたびに、必要なファイルを読み、直して、書き戻す (一時ファイル経由で置き換える)。読めなかったときは、書かずに例外にする (読めなかっただけの既存のデータを、空で上書きしない)。
/// 番号は、削除済みも含めた最大 + 1。削除は論理削除だけで、ファイルには残す。
/// </remarks>
public sealed class JsonWorkItemRepository(IJsonFileStore store) : IWorkItemRepository
{
    /// <summary>行の保存先のファイル名</summary>
    private const string ItemsFileName = "WorkItems.json";

    /// <summary>日ごとの記録の保存先のファイル名</summary>
    private const string RecordsFileName = "WorkRecords.json";

    /// <summary>進捗度のラベルの保存先のファイル名</summary>
    private const string ProgressFileName = "WorkProgressLevels.json";

    /// <summary>読み書きを順番に行うためのロック</summary>
    private readonly SemaphoreSlim _lock = new(1, 1);

    /// <inheritdoc />
    /// <remarks>ローカルの保存先は 1 人用なので、画面から編集できる。</remarks>
    public bool CanEditProgressLabels => true;

    /// <inheritdoc />
    public async Task<DataLoadResult<WorkItemSnapshot>> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var messages = new List<string>();
            var items = await ReadItemsAsync(messages, cancellationToken);
            var records = await ReadRecordsAsync(messages, cancellationToken);
            var labels = await ReadLabelsAsync(messages, cancellationToken);

            var alive = items.Where(i => !i.IsDeleted).ToList();
            var aliveIds = alive.Select(i => i.Id).ToHashSet();
            var snapshot = new WorkItemSnapshot(
                alive,
                [.. records.Where(r => aliveIds.Contains(r.WorkItemId))],
                labels.Where(l => l.Label.Length > 0).ToDictionary(l => l.Value, l => l.Label));
            return new DataLoadResult<WorkItemSnapshot>(snapshot, messages.Count == 0 ? null : string.Join(Environment.NewLine, messages));
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<WorkItem> AddAsync(WorkItem item, IReadOnlyList<WorkItemPosition> siblingPositions, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var items = await ReadItemsAsync([], cancellationToken);
            var saved = item with { Id = items.Count == 0 ? 1 : items.Max(i => i.Id) + 1, IsDeleted = false };
            ApplyPositions(items, siblingPositions);
            items.Add(saved);
            await store.WriteAsync(ItemsFileName, new WorkItemFile { Items = items }, WorkItemJsonContext.Readable.WorkItemFile, cancellationToken);
            return saved;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task UpdateAsync(WorkItem item, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var items = await ReadItemsAsync([], cancellationToken);
            var index = items.FindIndex(i => i.Id == item.Id);
            if (index < 0)
            {
                return;
            }

            // 並び・親・種類は、ここでは変えない (位置の更新で行う)
            items[index] = items[index] with
            {
                Name = item.Name,
                Note = item.Note,
                Priority = item.Priority,
                StartDate = item.StartDate,
                DueDate = item.DueDate,
                Status = item.Status,
                Progress = item.Progress,
                PlannedHours = item.PlannedHours,
            };
            await store.WriteAsync(ItemsFileName, new WorkItemFile { Items = items }, WorkItemJsonContext.Readable.WorkItemFile, cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task UpdatePositionsAsync(IReadOnlyList<WorkItemPosition> positions, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var items = await ReadItemsAsync([], cancellationToken);
            ApplyPositions(items, positions);
            await store.WriteAsync(ItemsFileName, new WorkItemFile { Items = items }, WorkItemJsonContext.Readable.WorkItemFile, cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task SetDeletedAsync(IReadOnlyList<int> ids, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var items = await ReadItemsAsync([], cancellationToken);
            var targets = ids.ToHashSet();
            for (var i = 0; i < items.Count; i++)
            {
                if (targets.Contains(items[i].Id))
                {
                    items[i] = items[i] with { IsDeleted = true };
                }
            }
            await store.WriteAsync(ItemsFileName, new WorkItemFile { Items = items }, WorkItemJsonContext.Readable.WorkItemFile, cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task SetRecordAsync(WorkRecord record, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var records = await ReadRecordsAsync([], cancellationToken);
            records.RemoveAll(r => r.WorkItemId == record.WorkItemId && r.Date == record.Date);
            if (!record.IsEmpty)
            {
                records.Add(record);
            }
            await store.WriteAsync(RecordsFileName, new WorkRecordFile { Items = records }, WorkItemJsonContext.Readable.WorkRecordFile, cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task SaveProgressLabelsAsync(IReadOnlyList<WorkProgressLabel> labels, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            await store.WriteAsync(ProgressFileName, new WorkProgressFile { Items = [.. labels] }, WorkItemJsonContext.Readable.WorkProgressFile, cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>行の位置を、行の一覧に反映する</summary>
    /// <param name="items">行の一覧 (書き換える)</param>
    /// <param name="positions">反映する位置</param>
    private static void ApplyPositions(List<WorkItem> items, IReadOnlyList<WorkItemPosition> positions)
    {
        foreach (var position in positions)
        {
            var index = items.FindIndex(i => i.Id == position.Id);
            if (index >= 0)
            {
                items[index] = items[index] with { ParentId = position.ParentId, SortOrder = position.SortOrder };
            }
        }
    }

    /// <summary>行のファイルを読む</summary>
    /// <param name="messages">壊れたファイルを退避したときのメッセージの追加先</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>行の一覧 (論理削除済みも含む。ファイルが無ければ空)</returns>
    private async Task<List<WorkItem>> ReadItemsAsync(List<string> messages, CancellationToken cancellationToken)
    {
        var result = await store.ReadAsync(ItemsFileName, WorkItemJsonContext.Readable.WorkItemFile, cancellationToken);
        AddMessage(messages, result.RecoveryMessage);
        return result.Value?.Items ?? [];
    }

    /// <summary>日ごとの記録のファイルを読む</summary>
    /// <param name="messages">壊れたファイルを退避したときのメッセージの追加先</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>記録の一覧 (ファイルが無ければ空)</returns>
    private async Task<List<WorkRecord>> ReadRecordsAsync(List<string> messages, CancellationToken cancellationToken)
    {
        var result = await store.ReadAsync(RecordsFileName, WorkItemJsonContext.Readable.WorkRecordFile, cancellationToken);
        AddMessage(messages, result.RecoveryMessage);
        return result.Value?.Items ?? [];
    }

    /// <summary>進捗度のラベルのファイルを読む。無ければ、初期値で作る</summary>
    /// <param name="messages">壊れたファイルを退避したときのメッセージの追加先</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>値ごとのラベル (0 から 100 の 11 件)</returns>
    private async Task<List<WorkProgressLabel>> ReadLabelsAsync(List<string> messages, CancellationToken cancellationToken)
    {
        var result = await store.ReadAsync(ProgressFileName, WorkItemJsonContext.Readable.WorkProgressFile, cancellationToken);
        AddMessage(messages, result.RecoveryMessage);
        if (result.Value?.Items is { Count: > 0 } saved)
        {
            return saved;
        }

        // 初めて開いたとき (壊れていて退避したときも)は、初期のラベルを入れる
        var initial = WorkProgress.Values
            .Select(v => new WorkProgressLabel { Value = v, Label = WorkProgress.DefaultLabels.GetValueOrDefault(v, "") })
            .ToList();
        await store.WriteAsync(ProgressFileName, new WorkProgressFile { Items = initial }, WorkItemJsonContext.Readable.WorkProgressFile, cancellationToken);
        return initial;
    }

    /// <summary>メッセージがあれば、一覧に足す</summary>
    /// <param name="messages">追加先</param>
    /// <param name="message">足すメッセージ (無ければ null)</param>
    private static void AddMessage(List<string> messages, string? message)
    {
        if (message is not null)
        {
            messages.Add(message);
        }
    }
}
