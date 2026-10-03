using Microsoft.Extensions.DependencyInjection;
using MmmTool.Views;

namespace MmmTool.Services;

/// <summary>リマインダーのメイン画面を開く</summary>
/// <remarks>
/// 画面はアプリ内で 1 枚だけ持ち、開いていれば前面に出す。閉じられたら次に開くときに作り直す。UI スレッドから呼ぶ。
/// </remarks>
public sealed class ReminderWindowService(IServiceProvider services, DialogService dialogs)
{
    /// <summary>開いている（開いている途中の）画面。無ければ null</summary>
    /// <remarks>読み込み中にもう一度開こうとしたとき、2 枚目を作らず、表示し終わるのを待ってから前面に出すため Task で持つ。</remarks>
    private Task<ReminderMainWindow>? _opening;

    /// <summary>開く（開いていれば前面に出す）</summary>
    /// <returns>開いた画面</returns>
    public async Task<ReminderMainWindow> ShowAsync()
    {
        if (_opening is { } opening)
        {
            var opened = await opening;
            opened.BringToFront();
            return opened;
        }

        _opening = OpenAsync();
        return await _opening;
    }

    /// <summary>作って表示する</summary>
    private async Task<ReminderMainWindow> OpenAsync()
    {
        var window = services.GetRequiredService<ReminderMainWindow>();
        window.Closed += (_, _) => _opening = null;
        // この画面から開くダイアログ（入力・一覧・確認）を、この画面の上に出すため
        dialogs.TrackWindow(window);
        await window.ShowAsync();
        return window;
    }

    /// <summary>通知から開く</summary>
    /// <remarks>通知をクリックして「気づいた」とみなし、発動済みで未対応のものをスヌーズに進める（次はスヌーズ間隔の後に再通知）。</remarks>
    public async Task ShowFromNotificationAsync()
    {
        var window = await ShowAsync();
        await window.ViewModel.SnoozeTriggeredAsync();
    }
}
