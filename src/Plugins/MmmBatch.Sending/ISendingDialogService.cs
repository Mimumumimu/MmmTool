namespace MmmBatch.Sending;

/// <summary>送信の状況の画面から、接続の設定の画面を開く (ViewModel から UI 型に触れずに使うための口。実装はホスト)</summary>
public interface ISendingDialogService
{
    /// <summary>DB の接続の設定の画面を、モーダルで開き、閉じるまで待つ</summary>
    /// <returns>画面が閉じるまでの待機を表すタスク</returns>
    Task ShowConnectionSettingsAsync();
}
