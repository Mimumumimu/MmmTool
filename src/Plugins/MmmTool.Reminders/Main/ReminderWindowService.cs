using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using MmmSdk.WinUI.Components.Dialogs;
using MmmSdk.WinUI.Utilities;

namespace MmmTool.Reminders.Main;

/// <summary>リマインダーのメイン画面を開く</summary>
/// <param name="scopeFactory">画面ごとの DI のスコープを作る</param>
/// <param name="dialogs">ダイアログの親を管理するサービス</param>
/// <remarks>
/// 画面はアプリ内で 1 枚だけ持ち、開いていれば前面に出す。閉じられたら次に開くときに作り直す。UI スレッドから呼ぶ。
/// 画面 (と ViewModel)は、開くたびに作るスコープから解決し、閉じたらスコープごと破棄する
/// (ルートから解決した <see cref="IDisposable"/> の Transient は、アプリの終了まで DI コンテナが保持し続けるため。通知をクリックするたびに開くので、積み上がる)。
/// </remarks>
public sealed class ReminderWindowService(IServiceScopeFactory scopeFactory, IDialogHost dialogs)
{
    /// <summary>開いている (開いている途中の)画面。無ければ null</summary>
    /// <remarks>読み込み中にもう一度開こうとしたとき、2 枚目を作らず、表示し終わるのを待ってから前面に出すため Task で持つ。</remarks>
    private Task<ReminderMainWindow>? _opening;

    /// <summary>開く (開いていれば前面に出す)</summary>
    /// <returns>開いた画面</returns>
    /// <remarks>画面を作る・表示するのに失敗したときは、例外をそのまま呼び出し元へ渡す (バグなので、安全網が受ける)。次に呼ぶときは、作り直す。</remarks>
    public async Task<ReminderMainWindow> ShowAsync()
    {
        if (_opening is { } opening)
        {
            var opened = await opening;
            // TEMP(計測): 原因が決まったら BringToFront() に戻す
            var trace = opened.BringToFrontTraced();
            WriteTrace($"開いている画面を前面に: クリックから {Stopwatch.GetElapsedTime(_clickedAt).TotalMilliseconds:F0}ms / {trace}");
            return opened;
        }

        _opening = OpenAsync();
        try
        {
            var created = await _opening;
            WriteTrace($"閉じていた画面を新しく開いた: クリックから {Stopwatch.GetElapsedTime(_clickedAt).TotalMilliseconds:F0}ms");
            return created;
        }
        catch
        {
            // 失敗した結果を残すと、以後の呼び出しが同じ失敗を待ち続けて二度と開けないため、作り直せる状態に戻して、例外はそのまま呼び出し元へ渡す
            _opening = null;
            throw;
        }
    }

    /// <summary>作って表示する</summary>
    /// <returns>開いた画面</returns>
    private async Task<ReminderMainWindow> OpenAsync()
    {
        var scope = scopeFactory.CreateScope();
        ReminderMainWindow window;
        try
        {
            window = scope.ServiceProvider.GetRequiredService<ReminderMainWindow>();
        }
        catch
        {
            scope.Dispose();
            throw;
        }

        window.Closed += (_, _) =>
        {
            _opening = null;
            scope.Dispose();
        };
        // この画面から開くダイアログ (入力・一覧・確認)を、この画面の上に出すため
        dialogs.TrackWindow(window);
        try
        {
            await window.ShowAsync();
        }
        catch
        {
            // 表示まで進まなかった作りかけの画面を残さない
            window.Close();
            throw;
        }
        return window;
    }

    /// <summary>TEMP(計測): 通知のクリックを受けた時刻 (<see cref="Stopwatch.GetTimestamp"/>)</summary>
    private long _clickedAt;

    /// <summary>TEMP(計測): 通知のクリックを受けた時刻を控える</summary>
    public void MarkClicked()
    {
        _clickedAt = Stopwatch.GetTimestamp();
        WriteTrace($"通知のクリックを受けた (画面は{(_opening is null ? "閉じている" : "開いている")})");
    }

    /// <summary>TEMP(計測): 前面化の結果を <c>Data/Logs/foreground-yyyy-MM-dd.log</c> に追記する</summary>
    /// <param name="message">書く内容</param>
    private static void WriteTrace(string message)
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "Data", "Logs");
        var now = DateTimeOffset.Now;
        try
        {
            Directory.CreateDirectory(directory);
            File.AppendAllText(Path.Combine(directory, $"foreground-{now:yyyy-MM-dd}.log"), $"[{now:HH:mm:ss.fff}] {message}{Environment.NewLine}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // 計測のログなので、書けなくても画面を開く処理は止めない
        }
    }

    /// <summary>通知から開く</summary>
    /// <returns>開いてスヌーズへ進める処理の完了を表すタスク</returns>
    /// <remarks>通知をクリックして「気づいた」とみなし、発動済みで未対応のものをスヌーズに進める (次はスヌーズ間隔の後に再通知)。</remarks>
    public async Task ShowFromNotificationAsync()
    {
        var window = await ShowAsync();
        await window.ViewModel.SnoozeTriggeredAsync();
    }
}
