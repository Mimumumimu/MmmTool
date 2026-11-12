using MmmSdk.WinUI.Components.Tray;
using MmmTool.Reminders.Main;

namespace MmmTool.Reminders;

/// <summary>トレイメニューの「リマインダー」</summary>
/// <param name="windows">リマインダーのメイン画面を開くサービス</param>
/// <remarks>押すとリマインダーのメイン画面を開く</remarks>
public sealed class ReminderTrayMenuSource(ReminderWindowService windows) : ITrayMenuSource
{
    /// <inheritdoc />
    public IReadOnlyList<TrayMenuItem> GetItems() => [TrayMenuItem.Command("リマインダー", windows.ShowAsync)];
}
