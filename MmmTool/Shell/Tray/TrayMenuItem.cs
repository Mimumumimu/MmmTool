namespace MmmTool.Shell.Tray;

/// <summary>
/// トレイメニューの 1 項目（文字とクリック時の処理の組、区切り線、またはサブメニュー）。
/// </summary>
public sealed class TrayMenuItem
{
    /// <summary>項目を作る</summary>
    /// <param name="text">表示する文字</param>
    /// <param name="invoked">クリック時の処理。無ければ null</param>
    /// <param name="children">サブメニューの項目。無ければ null</param>
    /// <param name="isEnabled">押せるか</param>
    /// <param name="isSeparator">区切り線か</param>
    /// <remarks>生成は <see cref="Command"/> などのメソッドから行う。</remarks>
    private TrayMenuItem(string text, Func<Task>? invoked, IReadOnlyList<TrayMenuItem>? children, bool isEnabled, bool isSeparator)
    {
        Text = text;
        Invoked = invoked;
        Children = children;
        IsEnabled = isEnabled;
        IsSeparator = isSeparator;
    }

    /// <summary>区切り線</summary>
    public static TrayMenuItem Separator { get; } = new("", null, null, false, true);

    /// <summary>表示する文字</summary>
    public string Text { get; }

    /// <summary>クリック時の処理。</summary>
    /// <remarks>失敗したら例外を投げる（トレイがメッセージを通知する）。</remarks>
    public Func<Task>? Invoked { get; }

    /// <summary>サブメニューの項目</summary>
    /// <remarks>1 件以上あればサブメニューとして出す。</remarks>
    public IReadOnlyList<TrayMenuItem>? Children { get; }

    /// <summary>押せるか</summary>
    public bool IsEnabled { get; }

    /// <summary>区切り線か</summary>
    public bool IsSeparator { get; }

    /// <summary>クリックで処理を行う項目。</summary>
    /// <param name="text">表示する文字</param>
    /// <param name="invoked">クリック時の処理</param>
    /// <returns>クリックで処理を行う項目</returns>
    public static TrayMenuItem Command(string text, Func<Task> invoked) => new(text, invoked, null, true, false);

    /// <summary>サブメニューを持つ項目</summary>
    /// <param name="text">表示する文字</param>
    /// <param name="children">サブメニューの項目</param>
    /// <returns>サブメニューを持つ項目</returns>
    public static TrayMenuItem Submenu(string text, IReadOnlyList<TrayMenuItem> children) => new(text, null, children, true, false);

    /// <summary>押せない項目（説明・状態の表示）。</summary>
    /// <param name="text">表示する文字</param>
    /// <returns>押せない項目</returns>
    public static TrayMenuItem Disabled(string text) => new(text, null, null, false, false);
}
