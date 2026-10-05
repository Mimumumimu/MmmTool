namespace MmmTool.Shell;

/// <summary>機能をオフにしてページを捨てるときに、画面の後始末が要るページ</summary>
/// <remarks>
/// <see cref="Main.PageProvider"/> が、オフにした機能のページをキャッシュから外すとき、ページを作ったスコープを破棄する前に呼ぶ。
/// <c>IDisposable</c> のサービス (ターミナルのセッションなど)の解放は、スコープの破棄が行うので、ここには、画面の部品に触れる後始末だけを書く。
/// <c>IDisposable</c> にしないのは、DI が、アプリの終了時にも <c>Dispose</c> を呼ぶため (そのとき画面の部品に触れない)。
/// </remarks>
public interface IReleasablePage
{
    /// <summary>画面の部品の後始末をする (ターミナルのコントロールからセッションを外すなど)</summary>
    void Release();
}
