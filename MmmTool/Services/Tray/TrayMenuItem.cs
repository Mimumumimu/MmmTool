namespace MmmTool.Services.Tray;

/// <summary>
/// トレイメニューの 1 項目（文字とクリック時の処理の組、区切り線、またはサブメニュー）。
/// </summary>
public sealed class TrayMenuItem
{
    private TrayMenuItem(string text, Func<Task>? invoked, IReadOnlyList<TrayMenuItem>? children, bool isEnabled, bool isSeparator)
    {
        Text = text;
        Invoked = invoked;
        Children = children;
        IsEnabled = isEnabled;
        IsSeparator = isSeparator;
    }

    public static TrayMenuItem Separator { get; } = new("", null, null, false, true);

    public string Text { get; }

    /// <summary>クリック時の処理。</summary>
    /// <remarks>失敗したら例外を投げる（トレイがメッセージを通知する）。</remarks>
    public Func<Task>? Invoked { get; }

    /// <summary>サブメニューの項目（1 件以上あればサブメニューとして出す）。</summary>
    public IReadOnlyList<TrayMenuItem>? Children { get; }

    public bool IsEnabled { get; }

    public bool IsSeparator { get; }

    /// <summary>クリックで処理を行う項目。</summary>
    public static TrayMenuItem Command(string text, Func<Task> invoked) => new(text, invoked, null, true, false);

    public static TrayMenuItem Submenu(string text, IReadOnlyList<TrayMenuItem> children) => new(text, null, children, true, false);

    /// <summary>押せない項目（説明・状態の表示）。</summary>
    public static TrayMenuItem Disabled(string text) => new(text, null, null, false, false);
}
