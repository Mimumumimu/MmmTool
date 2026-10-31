using System.Data;
using Microsoft.Data.SqlClient;
using MmmSdk.Core.Components.Storage;
using MmmTool.Data.Notes;
using MmmTool.Data.SqlServer.Connection;
using MmmTool.Notes.Core;
using MmmTool.Users.Core;

namespace MmmTool.Data.SqlServer.Notes;

/// <summary>
/// ノートを、SQL Server の <c>dbo.Note</c> に保存する。
/// </summary>
/// <param name="database">SQL Server への入口</param>
/// <param name="currentUser">今のユーザー</param>
/// <remarks>
/// 読むたびに DB から読む (表示している日の分だけ)。操作した人 (作成者・更新者・書いた人)は <see cref="CurrentUser"/>。特定していないと、読み書きできない。
/// 読み書きの SQL は、すべて「見える」条件 (全員に見えるものか、自分が書いたもの)を持つ。見えないノートを、番号だけで操作させないため。
/// 「自分だけ」の切り替えは、書いた人だけができる (ほかの人が、全員に見えるノートを、自分だけのものにして隠さないため)。
/// 更新日時は、更新の SQL の中で DB サーバーの時計を使って書く。
/// </remarks>
public sealed class SqlServerNoteRepository(SqlServerDatabase database, CurrentUser currentUser) : INoteRepository
{
    /// <summary>オブジェクト名が正しくない (表が無い)ときの SQL Server のエラー番号</summary>
    private const int InvalidObjectName = 208;

    /// <summary>列名が正しくない (列が無い)ときの SQL Server のエラー番号</summary>
    private const int InvalidColumnName = 207;

    /// <summary>表の全部の列 (読む順)</summary>
    private const string Columns = "Id, IsDeleted, CreatedAt, CreatedByUserId, UpdatedAt, UpdatedByUserId, AuthorUserId, IsPrivate, StartDate, EndDate, EventDate, Priority, Title, Body";

    /// <summary>表の全部の列 (読む順)に、表の別名を付けたもの</summary>
    /// <param name="alias">ノートの表の別名。定数だけを渡す (SQL に埋め込むため)</param>
    /// <returns>別名つきの列の並び (14 列)</returns>
    private static string QualifiedColumns(string alias) => string.Join(", ", Columns.Split(", ").Select(column => $"{alias}.{column}"));

    /// <inheritdoc />
    public bool IsShared => true;

    /// <inheritdoc />
    public int CurrentUserId => currentUser.Id;

    /// <inheritdoc />
    /// <exception cref="DataFileException">ユーザーを特定していない・設定が足りない・接続できない・表が無い・読めなかった。メッセージは画面に出せる。</exception>
    /// <remarks>書いた人と更新した人の名前は、ノートと一緒に <c>dbo.AppUser</c> を外部結合して読む。表示のための名前で、検索や索引には使わない。</remarks>
    public Task<IReadOnlyList<NoteWithNames>> GetOnDayAsync(DateOnly day, bool includeDeleted, CancellationToken cancellationToken = default)
    {
        var userId = RequireUserId();
        return RunAsync<IReadOnlyList<NoteWithNames>>(async connection =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText =
                $"""
                SELECT {QualifiedColumns("n")}, ISNULL(a.DisplayName, N''), ISNULL(u.DisplayName, N'')
                FROM dbo.Note AS n
                LEFT JOIN dbo.AppUser AS a ON a.Id = n.AuthorUserId
                LEFT JOIN dbo.AppUser AS u ON u.Id = n.UpdatedByUserId
                WHERE n.EndDate >= @Day AND n.StartDate <= @Day
                  AND (@IncludeDeleted = 1 OR n.IsDeleted = 0)
                  AND (n.IsPrivate = 0 OR n.AuthorUserId = @UserId)
                """;
            command.Parameters.Add(new SqlParameter("@Day", SqlDbType.Date) { Value = day.ToDateTime(TimeOnly.MinValue) });
            command.Parameters.Add(new SqlParameter("@IncludeDeleted", SqlDbType.Bit) { Value = includeDeleted });
            command.Parameters.Add(new SqlParameter("@UserId", SqlDbType.Int) { Value = userId });
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            var notes = new List<NoteWithNames>();
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                notes.Add(new NoteWithNames(ReadRow(reader).ToNote(), reader.GetString(14), reader.GetString(15)));
            }
            return notes;
        }, cancellationToken);
    }

    /// <inheritdoc />
    /// <exception cref="DataFileException">ユーザーを特定していない・設定が足りない・接続できない・表が無い・読めなかった。メッセージは画面に出せる。</exception>
    public Task<IReadOnlyList<Note>> GetOnEventDateAsync(DateOnly day, CancellationToken cancellationToken = default)
    {
        var userId = RequireUserId();
        return RunAsync<IReadOnlyList<Note>>(async connection =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText =
                $"""
                SELECT {Columns}
                FROM dbo.Note
                WHERE EventDate = @Day AND IsDeleted = 0
                  AND (IsPrivate = 0 OR AuthorUserId = @UserId)
                """;
            command.Parameters.Add(new SqlParameter("@Day", SqlDbType.Date) { Value = day.ToDateTime(TimeOnly.MinValue) });
            command.Parameters.Add(new SqlParameter("@UserId", SqlDbType.Int) { Value = userId });
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            var notes = new List<Note>();
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                notes.Add(ReadRow(reader).ToNote());
            }
            return notes;
        }, cancellationToken);
    }

    /// <inheritdoc />
    /// <exception cref="DataFileException">ユーザーを特定していない・設定が足りない・接続できない・表が無い・読めなかった。メッセージは画面に出せる。</exception>
    public Task<NoteChange?> FindAsync(int id, CancellationToken cancellationToken = default)
    {
        var userId = RequireUserId();
        return RunAsync<NoteChange?>(async connection =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT Id, Title, UpdatedByUserId, CreatedAt, UpdatedAt, StartDate, EndDate
                FROM dbo.Note
                WHERE Id = @Id AND IsDeleted = 0 AND (IsPrivate = 0 OR AuthorUserId = @UserId)
                """;
            command.Parameters.Add(new SqlParameter("@Id", SqlDbType.Int) { Value = id });
            command.Parameters.Add(new SqlParameter("@UserId", SqlDbType.Int) { Value = userId });
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            return await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
                ? new NoteChange(
                    reader.GetInt32(0), reader.GetString(1), reader.GetInt32(2), reader.GetDateTimeOffset(3), reader.GetDateTimeOffset(4),
                    DateOnly.FromDateTime(reader.GetDateTime(5)), DateOnly.FromDateTime(reader.GetDateTime(6)))
                : null;
        }, cancellationToken);
    }

    /// <inheritdoc />
    /// <remarks>ユーザーには依らない。読み方は、MmmBatch と共通の <see cref="SqlServerNoteChangeReader"/> に持つ。</remarks>
    /// <exception cref="DataFileException">設定が足りない・接続できない・表が無い・読めなかった。メッセージは画面に出せる。</exception>
    public Task<NoteChanges> GetChangesAsync(DateTimeOffset? since, DateOnly today, CancellationToken cancellationToken = default)
        => new SqlServerNoteChangeReader(database).ReadAsync(since, today, cancellationToken);

    /// <inheritdoc />
    /// <exception cref="ArgumentException">入力内容が正しくない。</exception>
    /// <exception cref="DataFileException">ユーザーを特定していない・設定が足りない・接続できない・表が無い・保存できなかった。メッセージは画面に出せる。</exception>
    public Task<Note> AddAsync(NoteInput input, CancellationToken cancellationToken = default)
    {
        NoteRow.Validate(input);
        var userId = RequireUserId();
        return RunAsync(async connection =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                INSERT INTO dbo.Note (CreatedByUserId, UpdatedByUserId, AuthorUserId, IsPrivate, StartDate, EndDate, EventDate, Priority, Title, Body)
                OUTPUT INSERTED.Id, INSERTED.UpdatedAt
                VALUES (@UserId, @UserId, @UserId, @IsPrivate, @StartDate, @EndDate, @EventDate, @Priority, @Title, @Body)
                """;
            command.Parameters.Add(new SqlParameter("@UserId", SqlDbType.Int) { Value = userId });
            AddInputParameters(command, input);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
            return new Note
            {
                Id = reader.GetInt32(0),
                AuthorUserId = userId,
                IsPrivate = input.IsPrivate,
                StartDate = input.StartDate,
                EndDate = input.EndDate,
                EventDate = input.EventDate,
                Priority = input.Priority,
                Title = input.Title,
                Body = input.Body,
                UpdatedAt = reader.GetDateTimeOffset(1),
                UpdatedByUserId = userId,
            };
        }, cancellationToken);
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentException">入力内容が正しくない。</exception>
    /// <exception cref="DataFileException">ユーザーを特定していない・設定が足りない・接続できない・表が無い・保存できなかった。メッセージは画面に出せる。</exception>
    public Task<bool> UpdateAsync(int id, NoteInput input, CancellationToken cancellationToken = default)
    {
        NoteRow.Validate(input);
        var userId = RequireUserId();
        return RunAsync(async connection =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                UPDATE dbo.Note
                SET IsPrivate = CASE WHEN AuthorUserId = @UserId THEN @IsPrivate ELSE IsPrivate END,
                    StartDate = @StartDate, EndDate = @EndDate, EventDate = @EventDate, Priority = @Priority, Title = @Title, Body = @Body,
                    UpdatedAt = SYSDATETIMEOFFSET(), UpdatedByUserId = @UserId
                WHERE Id = @Id AND (IsPrivate = 0 OR AuthorUserId = @UserId)
                """;
            command.Parameters.Add(new SqlParameter("@UserId", SqlDbType.Int) { Value = userId });
            command.Parameters.Add(new SqlParameter("@Id", SqlDbType.Int) { Value = id });
            AddInputParameters(command, input);
            return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) > 0;
        }, cancellationToken);
    }

    /// <inheritdoc />
    /// <exception cref="DataFileException">ユーザーを特定していない・設定が足りない・接続できない・表が無い・保存できなかった。メッセージは画面に出せる。</exception>
    public Task<bool> SetDeletedAsync(int id, bool isDeleted, CancellationToken cancellationToken = default)
    {
        var userId = RequireUserId();
        return RunAsync(async connection =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                UPDATE dbo.Note
                SET IsDeleted = @IsDeleted, UpdatedAt = SYSDATETIMEOFFSET(), UpdatedByUserId = @UserId
                WHERE Id = @Id AND IsDeleted <> @IsDeleted AND (IsPrivate = 0 OR AuthorUserId = @UserId)
                """;
            command.Parameters.Add(new SqlParameter("@IsDeleted", SqlDbType.Bit) { Value = isDeleted });
            command.Parameters.Add(new SqlParameter("@UserId", SqlDbType.Int) { Value = userId });
            command.Parameters.Add(new SqlParameter("@Id", SqlDbType.Int) { Value = id });
            return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) > 0;
        }, cancellationToken);
    }

    /// <summary>入力内容のパラメーターを足す (<c>@IsPrivate</c> <c>@StartDate</c> <c>@EndDate</c> <c>@EventDate</c> <c>@Priority</c> <c>@Title</c> <c>@Body</c>)</summary>
    /// <param name="command">コマンド</param>
    /// <param name="input">入力内容</param>
    private static void AddInputParameters(SqlCommand command, NoteInput input)
    {
        command.Parameters.Add(new SqlParameter("@IsPrivate", SqlDbType.Bit) { Value = input.IsPrivate });
        command.Parameters.Add(new SqlParameter("@StartDate", SqlDbType.Date) { Value = input.StartDate.ToDateTime(TimeOnly.MinValue) });
        command.Parameters.Add(new SqlParameter("@EndDate", SqlDbType.Date) { Value = input.EndDate.ToDateTime(TimeOnly.MinValue) });
        command.Parameters.Add(new SqlParameter("@EventDate", SqlDbType.Date) { Value = input.EventDate.ToDateTime(TimeOnly.MinValue) });
        command.Parameters.Add(new SqlParameter("@Priority", SqlDbType.TinyInt) { Value = (byte)input.Priority });
        command.Parameters.Add(new SqlParameter("@Title", SqlDbType.NVarChar, NoteLimits.TitleMaxLength) { Value = input.Title });

        // 本文は上限を決めない (nvarchar(max))。サイズ -1 が max
        command.Parameters.Add(new SqlParameter("@Body", SqlDbType.NVarChar, -1) { Value = input.Body });
    }

    /// <summary>今のユーザーの番号を取得する</summary>
    /// <returns>今のユーザーの番号</returns>
    /// <exception cref="DataFileException">ユーザーを特定していない (ログインが必要)。</exception>
    private int RequireUserId()
    {
        const string Message = "ユーザーが特定されていないため、ノートを使えません (ログインが必要です)。";
        return currentUser.User?.Id ?? throw new DataFileException(Message, new InvalidOperationException(Message));
    }

    /// <summary>接続を開いて、ノートの表を使う処理を実行する</summary>
    /// <typeparam name="T">処理の結果の型</typeparam>
    /// <param name="work">開いた接続で行う処理</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>処理の結果</returns>
    /// <exception cref="DataFileException">設定が足りない・接続できない・表が無い・実行中に失敗した。メッセージは画面に出せる。</exception>
    /// <remarks>表は、管理者が <c>003_AddNote.sql</c> を流して作る。まだ流していないとき、SQL のエラーのまま出さず、流す手順を伝える。</remarks>
    private Task<T> RunAsync<T>(Func<SqlConnection, Task<T>> work, CancellationToken cancellationToken)
        => database.RunAsync(async connection =>
        {
            try
            {
                return await work(connection).ConfigureAwait(false);
            }
            catch (SqlException ex) when (ex.Number == InvalidObjectName)
            {
                throw new DataFileException("ノートの表がデータベースにありません。管理者に、003_AddNote.sql を実行してもらってください。", ex);
            }
            catch (SqlException ex) when (ex.Number == InvalidColumnName)
            {
                throw new DataFileException("ノートの表に実施日の列がありません。管理者に、005_AddNoteEventDate.sql を実行してもらってください。", ex);
            }
        }, cancellationToken);

    /// <summary>ノートの行を読む (<see cref="Columns"/> の順)</summary>
    /// <param name="reader">読み取り中の結果</param>
    /// <returns>行</returns>
    private static NoteRow ReadRow(SqlDataReader reader) => new()
    {
        Id = reader.GetInt32(0),
        IsDeleted = reader.GetBoolean(1),
        CreatedAt = reader.GetDateTimeOffset(2),
        CreatedByUserId = reader.GetInt32(3),
        UpdatedAt = reader.GetDateTimeOffset(4),
        UpdatedByUserId = reader.GetInt32(5),
        AuthorUserId = reader.GetInt32(6),
        IsPrivate = reader.GetBoolean(7),
        StartDate = DateOnly.FromDateTime(reader.GetDateTime(8)),
        EndDate = DateOnly.FromDateTime(reader.GetDateTime(9)),
        EventDate = DateOnly.FromDateTime(reader.GetDateTime(10)),
        Priority = reader.GetByte(11),
        Title = reader.GetString(12),
        Body = reader.GetString(13),
    };
}
