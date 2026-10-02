using Microsoft.Extensions.DependencyInjection;
using MmmTool.Core.Services;
using MmmTool.Views;

namespace MmmTool.Services;

/// <summary>
/// <see cref="INotificationDialogService"/> の実装。通知ウィンドウを 1 枚だけ持つ。
/// </summary>
/// <remarks>ウィンドウは初回表示時に作る。ユーザーが閉じたら破棄し、次の通知で作り直す。</remarks>
public sealed class NotificationDialogService(IServiceProvider services) : INotificationDialogService
{
    /// <summary>通知ウィンドウ。まだ作っていない（または閉じられた）なら null</summary>
    private NotificationWindow? _window;

    /// <inheritdoc />
    public void Show(string title, IReadOnlyList<NotificationItem> items, Action? onClicked = null, string positionKey = INotificationDialogService.DefaultPositionKey)
    {
        if (_window is null)
        {
            var window = services.GetRequiredService<NotificationWindow>();
            window.Closed += (_, _) =>
            {
                if (ReferenceEquals(_window, window)) _window = null;
            };
            _window = window;
        }

        _window.Present(title, items, onClicked, positionKey);
    }

    /// <inheritdoc />
    public void Show(string title, string message, Action? onClicked = null, string positionKey = INotificationDialogService.DefaultPositionKey) =>
        Show(title, [new NotificationItem(message)], onClicked, positionKey);
}
