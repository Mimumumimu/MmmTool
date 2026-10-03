namespace MmmTool.Services.Tray;

/// <summary>
/// トレイメニューの「リマインダー」。押すとリマインダーのメイン画面を開く。
/// </summary>
public sealed class ReminderTrayMenuSource(ReminderWindowService windows) : ITrayMenuSource
{
    /// <inheritdoc />
    public IReadOnlyList<TrayMenuItem> GetItems() => [TrayMenuItem.Command("リマインダー", windows.ShowAsync)];
}
