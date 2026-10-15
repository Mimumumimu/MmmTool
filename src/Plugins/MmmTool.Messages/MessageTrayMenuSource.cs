using MmmSdk.WinUI.Components.Tray;
using MmmTool.Messages.Main;

namespace MmmTool.Messages;

/// <summary>トレイメニューの「メッセージを送る」。押すと送信ウィンドウを開く</summary>
/// <param name="windows">送信ウィンドウを開くサービス</param>
/// <remarks>サーバーモードでないとき・機能がオフのときは、機能の登録 (<see cref="MessagesFeature.Key"/>)により、メニューに出ない。</remarks>
public sealed class MessageTrayMenuSource(MessageWindowService windows) : ITrayMenuSource
{
    /// <inheritdoc />
    public IReadOnlyList<TrayMenuItem> GetItems() => [TrayMenuItem.Command("メッセージ送信", windows.ShowAsync)];
}
