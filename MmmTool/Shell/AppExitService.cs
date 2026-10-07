namespace MmmTool.Shell;

/// <summary>
/// 画面から、アプリの終了を頼む口。アプリ全体で 1 つ。
/// </summary>
/// <remarks>終了の処理そのもの (各機能の後始末→トレイアイコンの解放→終了)は <c>App</c> が持つ。ここは、頼む側 (設定ページなど)と <c>App</c> をつなぐだけ。トレイメニューの「終了」と同じ処理になる。</remarks>
public sealed class AppExitService
{
    /// <summary>終了が頼まれた</summary>
    public event EventHandler? ExitRequested;

    /// <summary>アプリの終了を頼む</summary>
    public void RequestExit() => ExitRequested?.Invoke(this, EventArgs.Empty);
}
