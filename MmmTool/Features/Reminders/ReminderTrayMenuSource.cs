using MmmSdk.WinUI.Components.Tray;
using MmmTool.Features.Reminders.Main;

namespace MmmTool.Features.Reminders;

/// <summary>
/// トレイメニューの「リマインダー」。押すとリマインダーのメイン画面を開く。
/// </summary>
/// <param name="windows">リマインダーのメイン画面を開くサービス</param>
public sealed class ReminderTrayMenuSource(ReminderWindowService windows) : ITrayMenuSource
{
    /// <inheritdoc />
    public IReadOnlyList<TrayMenuItem> GetItems() => [TrayMenuItem.Command("リマインダー", windows.ShowAsync)];
}
