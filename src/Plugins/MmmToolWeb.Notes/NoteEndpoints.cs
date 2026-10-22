using System.Globalization;
using System.Text;
using MmmTool.Notes.Core;
using MmmTool.Users.Core;
using MmmToolWeb.Features.Client;
using MmmToolWeb.Notes.Client;

namespace MmmToolWeb.Notes;

/// <summary>
/// ノートの API <c>/api/notes</c>。
/// </summary>
internal static class NoteEndpoints
{
    /// <summary>ノートが見つからないときの理由。</summary>
    private const string NotFoundMessage = "このノートは、見つからないか、ほかの人が先に変えました。";

    /// <summary>
    /// ノートの API を登録する。
    /// </summary>
    /// <param name="notes">登録先の <c>/api/notes</c>。</param>
    public static void Map(RouteGroupBuilder notes)
    {
        notes.MapGet("/", LoadAsync);
        notes.MapPost("/", AddAsync);
        notes.MapPut("/{id:int}", UpdateAsync);
        notes.MapPost("/{id:int}/deleted", SetDeletedAsync);
        notes.MapGet("/styles.css", GetStyles);
        notes.MapGet("/contents", LoadContentSourcesAsync);
        notes.MapGet("/contents/{key}", LoadContentAsync);
    }

    /// <summary>
    /// ノートの画面に出せる、他の機能の内容 (作業報告など)を返す。
    /// </summary>
    /// <param name="day">表示する日。実績の開始日の既定を、この日で決める。</param>
    /// <param name="contents">他の機能の内容を集める処理。</param>
    /// <param name="cancellationToken">キャンセルの通知。</param>
    /// <returns>出せる内容の一覧。サーバーに登録された機能の分だけ。</returns>
    private static async Task<IResult> LoadContentSourcesAsync(DateOnly day, DayContentService contents, CancellationToken cancellationToken)
    {
        var sources = new List<DayContentSourceDto>();
        foreach (var source in contents.Sources)
        {
            var from = await source.GetDefaultFromAsync(day, cancellationToken);
            sources.Add(new DayContentSourceDto(
                source.Key,
                source.Title,
                from,
                [.. source.Options.Select(option => new DayContentOptionDto(option.Key, option.Label, option.Default))]));
        }
        return Results.Ok(new DayContentSourceListResponse(sources));
    }

    /// <summary>
    /// 他の機能の内容を、枠にして返す。
    /// </summary>
    /// <param name="key">内容の識別。</param>
    /// <param name="day">表示する日で、期間の終わり。</param>
    /// <param name="from">実績の開始日で、期間の始まり。</param>
    /// <param name="everyone">全員の分を出すか。</param>
    /// <param name="on">選んでいる選択肢の識別を、カンマでつないだ文字。無い識別は、選んでいない。</param>
    /// <param name="contents">他の機能の内容を集める処理。</param>
    /// <param name="notes">ノートの読み書き。自分の番号を引く。</param>
    /// <param name="cancellationToken">キャンセルの通知。</param>
    /// <returns>枠の一覧。内容の識別が無いときは 404。</returns>
    private static async Task<IResult> LoadContentAsync(
        string key, DateOnly day, DateOnly from, bool? everyone, string? on, DayContentService contents, NoteService notes,
        CancellationToken cancellationToken)
    {
        var source = contents.Sources.FirstOrDefault(s => s.Key == key);
        if (source is null)
        {
            return Results.NotFound(new ErrorResponse("この内容は、見つかりません。"));
        }

        var onKeys = (on ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries);
        var options = source.Options.ToDictionary(option => option.Key, option => onKeys.Contains(option.Key));
        var all = everyone ?? false;
        var frames = await contents.LoadAsync(source, from, day, all, all ? notes.CurrentUserId : 0, options, cancellationToken);
        return Results.Ok(new DayContentFramesResponse([.. frames.Select(frame => new DayContentFrameDto(frame.Title, frame.Text))]));
    }

    /// <summary>
    /// ある日に有効なノートを返す。
    /// </summary>
    /// <param name="day">日。</param>
    /// <param name="includeDeleted">削除したものも含めるか。</param>
    /// <param name="service">ノートの読み書き。</param>
    /// <param name="formatter">本文を、表示してよい HTML にする処理。</param>
    /// <param name="currentUser">今のユーザー。</param>
    /// <param name="cancellationToken">キャンセルの通知。</param>
    /// <returns>ノートの一覧。</returns>
    private static async Task<IResult> LoadAsync(
        DateOnly day, bool? includeDeleted, NoteService service, INoteBodyFormatter formatter, CurrentUser currentUser,
        CancellationToken cancellationToken)
    {
        var cards = await service.LoadAsync(day, includeDeleted ?? false, cancellationToken);
        var userId = currentUser.Id;
        return Results.Ok(new NoteListResponse(day, [.. cards.Select(card => ToDto(card, userId, formatter))]));
    }

    /// <summary>
    /// ノートを追加する。
    /// </summary>
    /// <param name="request">保存する内容。</param>
    /// <param name="service">ノートの読み書き。</param>
    /// <param name="cancellationToken">キャンセルの通知。</param>
    /// <returns>成功なら 204。入力が正しくないときは 400。</returns>
    private static async Task<IResult> AddAsync(NoteSaveRequest request, NoteService service, CancellationToken cancellationToken)
    {
        var input = ToInput(request);
        if (input.Validate() is { } error)
        {
            return Results.BadRequest(new ErrorResponse(error));
        }
        await service.AddAsync(input, cancellationToken);
        return Results.NoContent();
    }

    /// <summary>
    /// ノートを変更する。
    /// </summary>
    /// <param name="id">ノートの番号。</param>
    /// <param name="request">保存する内容。</param>
    /// <param name="service">ノートの読み書き。</param>
    /// <param name="cancellationToken">キャンセルの通知。</param>
    /// <returns>成功なら 204。入力が正しくないときは 400。見えない・無いときは 404。</returns>
    private static async Task<IResult> UpdateAsync(int id, NoteSaveRequest request, NoteService service, CancellationToken cancellationToken)
    {
        var input = ToInput(request);
        if (input.Validate() is { } error)
        {
            return Results.BadRequest(new ErrorResponse(error));
        }
        return await service.UpdateAsync(id, input, cancellationToken)
            ? Results.NoContent()
            : Results.NotFound(new ErrorResponse(NotFoundMessage));
    }

    /// <summary>
    /// ノートを削除する・復元する。
    /// </summary>
    /// <param name="id">ノートの番号。</param>
    /// <param name="request">削除か復元か。</param>
    /// <param name="service">ノートの読み書き。</param>
    /// <param name="cancellationToken">キャンセルの通知。</param>
    /// <returns>成功なら 204。見えない・無い・すでにその状態のときは 404。</returns>
    /// <remarks>状態を変える操作は、JSON の本文を要る形にする。ほかのサイトの画面から、本文のないフォームの送信で呼ばれないため。</remarks>
    private static async Task<IResult> SetDeletedAsync(int id, NoteDeletedRequest request, NoteService service, CancellationToken cancellationToken)
        => await service.SetDeletedAsync(id, request.IsDeleted, cancellationToken)
            ? Results.NoContent()
            : Results.NotFound(new ErrorResponse(NotFoundMessage));

    /// <summary>
    /// 本文の色のスタイルシートを返す。色の値は、パレット (<see cref="NoteColorPalette"/>)から作る。
    /// </summary>
    /// <returns>スタイルシート。ライトの値と、ダークのときに差し替える値を持つ。</returns>
    /// <remarks>
    /// 本文の整形が出す色は、<c>nc</c> に番号を付けた名前の指定だけ。色の値を本文に直接書かないので、ページ側の方針 (インラインのスタイルを許さない)を守れる。
    /// </remarks>
    private static IResult GetStyles()
    {
        var css = new StringBuilder();
        foreach (var color in NoteColorPalette.All)
        {
            css.Append(".nc").Append(color.Index.ToString(CultureInfo.InvariantCulture)).Append(" { color: ").Append(color.Hex).Append("; }\n");
        }
        css.Append("@media (prefers-color-scheme: dark) {\n");
        foreach (var color in NoteColorPalette.All)
        {
            css.Append("  .nc").Append(color.Index.ToString(CultureInfo.InvariantCulture)).Append(" { color: ").Append(color.DarkHex).Append("; }\n");
        }
        css.Append("}\n");
        return Results.Text(css.ToString(), "text/css; charset=utf-8");
    }

    /// <summary>
    /// 画面に返す形にする。本文は、表示してよい HTML にして添える。
    /// </summary>
    /// <param name="card">カードに出す内容。</param>
    /// <param name="userId">今のユーザーの番号。</param>
    /// <param name="formatter">本文を、表示してよい HTML にする処理。</param>
    /// <returns>画面に返す 1 件。</returns>
    private static NoteDto ToDto(NoteCard card, int userId, INoteBodyFormatter formatter)
    {
        var note = card.Note;
        return new NoteDto(
            note.Id,
            note.Title,
            note.Body,
            formatter.ToHtml(note.Body),
            (int)note.Priority,
            note.StartDate,
            note.IsOpenEnded ? null : note.EndDate,
            note.IsPrivate,
            note.IsDeleted,
            note.AuthorUserId == userId,
            card.AuthorName,
            card.IsUpdatedByOther ? card.UpdatedByName : null,
            note.UpdatedAt.ToLocalTime().ToString("yyyy/MM/dd HH:mm", CultureInfo.InvariantCulture),
            card.IsUpdatedToday);
    }

    /// <summary>
    /// 要求を、保存する入力の形にする。
    /// </summary>
    /// <param name="request">要求。</param>
    /// <returns>入力の内容。</returns>
    private static NoteInput ToInput(NoteSaveRequest request) => new()
    {
        Title = request.Title,
        Body = request.Body,
        Priority = (NotePriority)request.Priority,
        IsPrivate = request.IsPrivate,
        StartDate = request.StartDate,
        EndDate = request.EndDate ?? DateOnly.MaxValue,
    };
}
