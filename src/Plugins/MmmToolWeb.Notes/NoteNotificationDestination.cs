using MmmTool.Notes.Core;
using MmmTool.NotificationChannels.Core;
using MmmToolWeb.Features.Client;

namespace MmmToolWeb.Notes;

/// <summary>ノートの通知を押したときにそのノートが有効な日でノートのページを開く行き先</summary>
/// <remarks>
/// 「通知」の機能はノートの名前を持たない。
/// この行き先を登録するのはノートの機能。
/// 開く日は今日が期間内なら今日、そうでなければ開始日。
/// 今日はサーバーの日付で決める。
/// ノートが見えない・削除済み・終了日が過去のときは日付を付けずに、ノートのページを開く。
/// </remarks>
/// <param name="repository">ノートの保存先</param>
/// <param name="time">現在時刻の取得元</param>
public sealed class NoteNotificationDestination(INoteRepository repository, TimeProvider time) : INotificationDestination
{
    /// <summary>ノートのページの道</summary>
    /// <remarks>NotesPage.Route と同じ</remarks>
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

        return $"{NotesPath}?date={ApiDay.ToText(note.DayToOpen(today))}";
    }
}
