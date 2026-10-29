using System.Globalization;
using MmmTool.Notes.Core;
using MmmTool.NotificationChannels.Core;

namespace MmmToolWeb.Notes;

/// <summary>
/// ノートの通知を押したときに、そのノートが有効な日で、ノートのページを開く行き先。
/// </summary>
/// <remarks>
/// 「通知」の機能は、ノートの名前を持たない。この行き先を登録するのは、ノートの機能。
/// 開く日は、今日が期間内なら今日、そうでなければ開始日。今日は、サーバーの日付で決める。
/// ノートが見えない・削除済み・終了日が過去のときは、日付を付けずに、ノートのページを開く。
/// </remarks>
/// <param name="repository">ノートの保存先</param>
/// <param name="time">現在時刻の取得元</param>
public sealed class NoteNotificationDestination(INoteRepository repository, TimeProvider time) : INotificationDestination
{
    /// <summary>ノートのページの道。<c>NotesPage.Route</c> と同じ</summary>
    private const string NotesPath = "/notes";

    /// <inheritdoc />
    public string Key => NoteNotificationText.ItemKind;

    /// <inheritdoc />
    public async Task<string?> GetPathAsync(int id, CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(time.GetLocalNow().DateTime);
        if (await repository.FindAsync(id, cancellationToken) is not { } note || note.EndDate < today)
        {
            return NotesPath;
        }

        return $"{NotesPath}?date={note.DayToOpen(today).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}";
    }
}
