namespace MmmTool.Shell;

/// <summary>メインウィンドウの × ボタンを押したときの動き</summary>
/// <remarks>
/// Alt+F4 を含む。
/// 値の名前を設定ストアに保存するので、決めたら変えない。
/// </remarks>
public enum CloseButtonAction
{
    /// <summary>ウィンドウを隠し、トレイに残る</summary>
    /// <remarks>アプリは動き続ける</remarks>
    Tray,

    /// <summary>アプリを終了する</summary>
    Exit,
}
