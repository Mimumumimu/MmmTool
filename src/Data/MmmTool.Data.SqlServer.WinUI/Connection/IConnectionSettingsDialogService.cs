namespace MmmTool.Data.SqlServer.WinUI.Connection;

/// <summary>画面の ViewModel から、DB の接続の設定の画面を開く口</summary>
/// <remarks>ViewModel が UI の型に触れずに使うための口。実装は <see cref="ConnectionSettingsDialogService"/> で、<c>AddConnectionSettingsDialog</c> が登録する。</remarks>
public interface IConnectionSettingsDialogService
{
    /// <summary>接続の設定の画面が閉じたときに起きる</summary>
    /// <remarks>保存して閉じたときも、キャンセルして閉じたときも起きる。DB を読む画面が、新しい設定で読み直すために使う。UI スレッドで起きる。</remarks>
    event EventHandler? ConnectionSettingsClosed;

    /// <summary>DB の接続の設定の画面を、モーダルで開き、閉じるまで待つ</summary>
    /// <returns>画面が閉じるまでの待機を表すタスク</returns>
    Task ShowConnectionSettingsAsync();
}
