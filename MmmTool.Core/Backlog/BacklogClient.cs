using System.Net;
using System.Text.Json;
using MmmTool.Core.Backlog.Json;

namespace MmmTool.Core.Backlog;

/// <summary>
/// Backlog の REST API (API キー認証)で、共有ファイルの一覧の取得とダウンロードを行う。
/// </summary>
/// <remarks>
/// <see cref="HttpClient"/> はアプリ全体で 1 つを使い回す (ソケットの枯渇を避けるため、このクラスを Singleton にする)。接続は定期的に張り直す (DNS の変更に追従するため)。
/// タイムアウトは長め (5 分)。ダウンロードは、応答の先頭を受けたあとはタイムアウトの対象にならないので、大きなファイルも取れる。
/// 通信は常に https。API キーはクエリに入るので、エラーのメッセージには URL を入れない。
/// 失敗は <see cref="BacklogException"/> で上げる (呼び出し元が画面に出す)。
/// </remarks>
public sealed class BacklogClient : IDisposable
{
    /// <summary>通信のタイムアウト</summary>
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(5);

    /// <summary>接続を張り直す間隔</summary>
    private static readonly TimeSpan ConnectionLifetime = TimeSpan.FromMinutes(10);

    /// <summary>通信に使う HttpClient</summary>
    private readonly HttpClient _http = new(new SocketsHttpHandler { PooledConnectionLifetime = ConnectionLifetime }) { Timeout = Timeout };

    /// <summary>共有ファイルの一覧を取得する (フォルダーは含めず、ファイルだけ)</summary>
    /// <param name="location">取得するフォルダーの場所</param>
    /// <param name="apiKey">API キー</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>フォルダー直下のファイル (API が返した順)</returns>
    /// <exception cref="BacklogException">通信に失敗した、または応答が想定の形ではなかった。</exception>
    public async Task<IReadOnlyList<BacklogSharedFile>> ListFilesAsync(BacklogLocation location, string apiKey, CancellationToken cancellationToken = default)
    {
        var folder = Uri.EscapeDataString(location.FolderPath);
        var url = $"https://{location.Domain}/api/v2/projects/{Uri.EscapeDataString(location.ProjectKey)}/files/metadata/{folder}?apiKey={Uri.EscapeDataString(apiKey)}";

        try
        {
            using var response = await _http.GetAsync(url, cancellationToken);
            ThrowIfFailed(response);

            await using var body = await response.Content.ReadAsStreamAsync(cancellationToken);
            var files = await JsonSerializer.DeserializeAsync(body, BacklogJsonContext.Default.ListBacklogSharedFile, cancellationToken);
            return (files ?? []).Where(file => file.IsFile).ToList();
        }
        catch (HttpRequestException ex)
        {
            throw ConnectionFailed(ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw TimedOut(ex);
        }
        catch (JsonException ex)
        {
            throw new BacklogException("Backlog の応答を読めませんでした。", ex);
        }
    }

    /// <summary>共有ファイルをダウンロードして、ファイルに保存する</summary>
    /// <param name="location">ファイルがあるフォルダーの場所 (スペースとプロジェクトを使う)</param>
    /// <param name="file">ダウンロードするファイル</param>
    /// <param name="apiKey">API キー</param>
    /// <param name="destinationPath">保存先のパス (同じ名前のファイルがあれば置き換える)</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>保存の完了を表すタスク</returns>
    /// <remarks>
    /// 保存先の隣の一時ファイルに書いてから置き換える (途中で失敗しても、元のファイルが壊れない)。メモリには溜めず、受けながら書く。
    /// 保存先に書けないとき (ロック・権限など)は、<see cref="IOException"/> / <see cref="UnauthorizedAccessException"/> をそのまま上げる。
    /// </remarks>
    /// <exception cref="BacklogException">通信に失敗した。</exception>
    public async Task DownloadAsync(BacklogLocation location, BacklogSharedFile file, string apiKey, string destinationPath, CancellationToken cancellationToken = default)
    {
        var url = $"https://{location.Domain}/api/v2/projects/{Uri.EscapeDataString(location.ProjectKey)}/files/{file.Id}?apiKey={Uri.EscapeDataString(apiKey)}";
        var temporaryPath = destinationPath + ".download";

        try
        {
            using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            ThrowIfFailed(response);

            try
            {
                await using (var output = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
                {
                    await response.Content.CopyToAsync(output, cancellationToken);
                }
                File.Move(temporaryPath, destinationPath, overwrite: true);
            }
            catch
            {
                // 書き込みが失敗しても、取り消されても、途中の一時ファイルは残さない (元の例外を優先する)
                DeleteQuietly(temporaryPath);
                throw;
            }
        }
        catch (HttpRequestException ex)
        {
            throw ConnectionFailed(ex);
        }
        catch (HttpIOException ex)
        {
            throw new BacklogException("ダウンロードの途中で接続が切れました。", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw TimedOut(ex);
        }
    }

    /// <inheritdoc />
    public void Dispose() => _http.Dispose();

    /// <summary>応答が失敗なら、理由を持つ例外を上げる</summary>
    /// <param name="response">API の応答</param>
    /// <exception cref="BacklogException">応答が成功ではなかった。</exception>
    private static void ThrowIfFailed(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        throw new BacklogException(response.StatusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => "API キーが正しくないか、このプロジェクトを見る権限がありません。設定ページの API キーを確かめてください。",
            HttpStatusCode.NotFound => "プロジェクトまたはフォルダーが見つかりません。URL を確かめてください。",
            HttpStatusCode.TooManyRequests => "Backlog への要求が多すぎます。しばらく待ってからやり直してください。",
            _ => $"Backlog がエラーを返しました (HTTP {(int)response.StatusCode})。",
        });
    }

    /// <summary>接続の失敗を、画面に出せる例外にする</summary>
    /// <param name="exception">元の例外</param>
    /// <returns>画面に出せるメッセージを持つ例外</returns>
    private static BacklogException ConnectionFailed(HttpRequestException exception)
        => new($"Backlog に接続できませんでした。{exception.Message}", exception);

    /// <summary>タイムアウトを、画面に出せる例外にする</summary>
    /// <param name="exception">元の例外</param>
    /// <returns>画面に出せるメッセージを持つ例外</returns>
    private static BacklogException TimedOut(TaskCanceledException exception)
        => new("Backlog からの応答がありませんでした (時間切れ)。", exception);

    /// <summary>一時ファイルを消す。消せなくても例外にしない</summary>
    /// <param name="path">消すファイルのパス</param>
    /// <remarks>後始末なので、元の失敗を隠さないよう、消せなかったことは無視する。</remarks>
    private static void DeleteQuietly(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // 元の例外を優先する
        }
    }
}
