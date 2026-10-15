using Microsoft.Extensions.DependencyInjection;
using MmmSdk.WinUI.Components.Dialogs;
using MmmSdk.WinUI.Utilities;

namespace MmmTool.Messages.Main;

/// <summary>メッセージ送信のウィンドウを開く</summary>
/// <param name="scopeFactory">画面ごとの DI のスコープを作る</param>
/// <param name="dialogs">ダイアログの親を管理するサービス</param>
/// <remarks>画面はアプリ内で 1 枚だけ持ち、開いていれば前面に出す。閉じられたら次に開くときに作り直す。UI スレッドから呼ぶ。</remarks>
public sealed class MessageWindowService(IServiceScopeFactory scopeFactory, IDialogHost dialogs)
{
    /// <summary>開いている画面。無ければ null</summary>
    private MessageSendWindow? _window;

    /// <summary>開く (開いていれば前面に出す)</summary>
    /// <returns>開く処理の完了を表すタスク</returns>
    public Task ShowAsync()
    {
        if (_window is { } opened)
        {
            opened.BringToFront();
            return Task.CompletedTask;
        }

        var scope = scopeFactory.CreateScope();
        var window = scope.ServiceProvider.GetRequiredService<MessageSendWindow>();
        window.Closed += (_, _) =>
        {
            _window = null;
            scope.Dispose();
        };
        dialogs.TrackWindow(window);
        _window = window;
        window.ShowWindow();
        return Task.CompletedTask;
    }
}
