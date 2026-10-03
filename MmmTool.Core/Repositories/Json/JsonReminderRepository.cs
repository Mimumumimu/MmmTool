using MmmSdk.Core.Repositories;
using MmmSdk.Core.Repositories.Json;
using MmmTool.Core.Entities;

namespace MmmTool.Core.Repositories.Json;

/// <summary>リマインダーを JSON ファイルに保存する</summary>
/// <param name="store">JSON ファイルの読み書き</param>
/// <remarks>本体は <c>Reminders.json</c>、対応状態は <c>ReminderStates.json</c> に分けて保存する（汎用設定ストアとは別）。</remarks>
public sealed class JsonReminderRepository(JsonFileStore store) : IReminderRepository
{
    /// <summary>本体の保存先のファイル名</summary>
    private const string RemindersFileName = "Reminders.json";

    /// <summary>対応状態の保存先のファイル名</summary>
    private const string StatesFileName = "ReminderStates.json";

    /// <inheritdoc />
    public async Task<DataLoadResult<List<Reminder>>> LoadRemindersAsync(CancellationToken cancellationToken = default)
    {
        var result = await store.ReadAsync(RemindersFileName, CoreJsonContext.Readable.ReminderFile, cancellationToken);
        return new(result.Value?.Items ?? [], result.RecoveryMessage);
    }

    /// <inheritdoc />
    public Task SaveRemindersAsync(IReadOnlyList<Reminder> reminders, CancellationToken cancellationToken = default)
        => store.WriteAsync(RemindersFileName, new ReminderFile { Items = [.. reminders] }, CoreJsonContext.Readable.ReminderFile, cancellationToken);

    /// <inheritdoc />
    public async Task<DataLoadResult<List<ReminderState>>> LoadStatesAsync(CancellationToken cancellationToken = default)
    {
        var result = await store.ReadAsync(StatesFileName, CoreJsonContext.Readable.ReminderStateFile, cancellationToken);
        return new(result.Value?.Items ?? [], result.RecoveryMessage);
    }

    /// <inheritdoc />
    public Task SaveStatesAsync(IReadOnlyList<ReminderState> states, CancellationToken cancellationToken = default)
        => store.WriteAsync(StatesFileName, new ReminderStateFile { Items = [.. states] }, CoreJsonContext.Readable.ReminderStateFile, cancellationToken);
}

/// <summary>Reminders.json の中身</summary>
/// <remarks>リンクの JSON と同じく <c>{ "items": [...] }</c> の形にする。</remarks>
internal sealed class ReminderFile
{
    /// <summary>リマインダー本体の一覧</summary>
    public List<Reminder>? Items { get; set; } = [];
}

/// <summary>ReminderStates.json の中身</summary>
internal sealed class ReminderStateFile
{
    /// <summary>対応状態の一覧</summary>
    public List<ReminderState>? Items { get; set; } = [];
}
