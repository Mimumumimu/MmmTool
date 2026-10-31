namespace MmmTool.Shell;

/// <summary>メインウィンドウの × ボタン (Alt+F4 を含む)を押したときの動き</summary>
/// <remarks>値の名前を設定ストアに保存するので、決めたら変えない。</remarks>
public enum CloseButtonAction
{
    /// <summary>ウィンドウを隠し、トレイに残る (アプリは動き続ける)</summary>
    Tray,

    /// <summary>アプリを終了する (トレイメニューの「終了」と同じ)</summary>
    Exit,
}
