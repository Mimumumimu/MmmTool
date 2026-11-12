namespace MmmTool.Shell;

/// <summary>画面からアプリの終了を頼む口</summary>
/// <remarks>
/// アプリ全体で 1 つ
/// 終了の処理そのもの (各機能の後始末→トレイアイコンの解放→終了)は <c>App</c> が持つ
/// ここは頼む側 (設定ページなど)と <c>App</c> をつなぐだけ
/// トレイメニューの「終了」と同じ処理になる
/// </remarks>
public sealed class AppExitService
{
    /// <summary>終了が頼まれた</summary>
    public event EventHandler? ExitRequested;

    /// <summary>アプリの終了を頼む</summary>
    public void RequestExit() => ExitRequested?.Invoke(this, EventArgs.Empty);
}
