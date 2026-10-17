using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Text.Json;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.Web.WebView2.Core;
using MmmSdk.WinUI.Components.WebViews;
using MmmTool.Notes.Core;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Streams;

namespace MmmTool.Notes.Main;

/// <summary>
/// その日のノートのカード一覧を、WebView2 の 1 枚のページで表示する。
/// </summary>
/// <remarks>
/// <para>
/// ページは、アプリが組み立てた HTML (<see cref="NotePageBuilder"/>)を、仮想ホスト (<see cref="NoteLinks.PageHost"/>)の <c>index.html</c> として返して表示する
/// (文字列で渡す方式には、渡せる量の上限があるが、本文は上限を決めないため)。
/// </para>
/// <para>
/// ページの中のスクリプトは止める。外へのリクエストはすべて止め (仮想ホスト以外は、失敗で返す)、ページの移動は、すべて横取りする。
/// カードの「編集」「削除」「復元」は <see cref="ActionRequested"/>、本文のリンク (http・https・file)は <see cref="LinkRequested"/> で知らせる。それ以外への移動は、取り消す。
/// 作り直す前の状態 (スクロールの位置・開いているカード)は、アプリ側から実行するスクリプトで取り出す (ページのスクリプトを止めても、これは動く)。
/// </para>
/// </remarks>
public sealed partial class NoteCardsView : UserControl, INoteCardsSurface
{
    /// <summary>右クリックのメニューの項目の最小の幅 (DIP。押しやすい幅にする)</summary>
    private const double MenuMinWidth = 160;

    /// <summary>ページに付ける、外部の読み込みを止める決まり (表示に使うのは、ページの中の CSS だけ)</summary>
    private const string ContentSecurityPolicy = "default-src 'none'; style-src 'unsafe-inline'; base-uri 'none'; form-action 'none'; frame-ancestors 'none'";

    /// <summary>表示するページの HTML</summary>
    private string _html = "";

    /// <summary>表示したあとに合わせる、スクロールの位置</summary>
    private double _pendingScroll;

    /// <summary>開いてよいリンク先 (今のページに出したリンクだけ)</summary>
    /// <remarks>ファイルをページに落として、そのファイルへ移ろうとしたとき (開くと、実行ファイルが動いてしまう)など、ページに出していない移動を、開かないため。</remarks>
    private IReadOnlySet<string> _allowedLinks = new HashSet<string>();

    /// <summary>初期化を始めたか</summary>
    private bool _initialized;

    /// <summary>ページを表示できる状態か (WebView2 の準備ができ、ページの読み込みが成功した)</summary>
    private bool _ready;

    /// <summary>WebView2 の準備ができたか</summary>
    private bool _webViewReady;

    /// <summary>WebView2 のデータ (キャッシュなど)の保存先フォルダー (読み込まれる前に設定する)</summary>
    public string WebView2Directory { get; set; } = "";

    /// <summary>カードの操作が押された</summary>
    public event EventHandler<NoteActionEventArgs>? ActionRequested;

    /// <summary>本文のリンクが押された (開いてよい種類 (http・https・file)のものだけ)</summary>
    public event EventHandler<Uri>? LinkRequested;

    /// <summary>表示面を作る</summary>
    public NoteCardsView()
    {
        InitializeComponent();
        // 背景を描かない (アプリの背景がそのまま見え、ライト・ダークで白や黒の四角が出ないように)
        WebView.DefaultBackgroundColor = Colors.Transparent;
        Loaded += OnLoaded;
    }

    /// <inheritdoc />
    public async Task<NoteViewState?> CaptureStateAsync()
    {
        if (!_ready)
        {
            return null;
        }

        const string Script =
            "JSON.stringify({y: document.documentElement.scrollTop || 0, open: Array.from(document.querySelectorAll('details[open]')).map(function (d) { return d.parentElement.id; }), known: Array.from(document.querySelectorAll('details')).map(function (d) { return d.parentElement.id; })})";
        try
        {
            // 結果は、JSON の文字列 (中身も JSON の文字)で返る
            var outer = await WebView.CoreWebView2.ExecuteScriptAsync(Script);
            var inner = JsonSerializer.Deserialize<string>(outer);
            if (string.IsNullOrEmpty(inner))
            {
                return null;
            }

            using var json = JsonDocument.Parse(inner);
            var open = ReadNoteIds(json.RootElement.GetProperty("open"));
            var known = ReadNoteIds(json.RootElement.GetProperty("known"));
            return new NoteViewState(json.RootElement.GetProperty("y").GetDouble(), open, known);
        }
        catch (Exception ex) when (ex is COMException or JsonException or InvalidOperationException)
        {
            // 状態を控えられなくても、作り直しは続けられる (位置が先頭に戻るだけ)
            return null;
        }
    }

    /// <summary>カードの id (<c>"n"</c> + ノートの番号)の配列から、ノートの番号の集まりにする</summary>
    /// <param name="ids">カードの id の配列</param>
    /// <returns>ノートの番号</returns>
    private static HashSet<int> ReadNoteIds(JsonElement ids)
    {
        var result = new HashSet<int>();
        foreach (var element in ids.EnumerateArray())
        {
            if (element.GetString() is { Length: > 1 } id && int.TryParse(id.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out var noteId))
            {
                result.Add(noteId);
            }
        }
        return result;
    }

    /// <inheritdoc />
    public void Show(string html, double scrollTop, IReadOnlySet<string> allowedLinks)
    {
        _html = html;
        _pendingScroll = scrollTop;
        _allowedLinks = allowedLinks;
        if (_webViewReady)
        {
            _ready = false;
            WebView.CoreWebView2.Navigate(NoteLinks.PageUrl);
        }
    }

    /// <summary>読み込み時に WebView2 を初期化する</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">イベントの情報</param>
    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_initialized)
        {
            return;
        }
        _initialized = true;

        try
        {
            await WebView2Setup.EnsureAsync(WebView, string.IsNullOrEmpty(WebView2Directory) ? null : WebView2Directory);
        }
        catch (COMException ex)
        {
            // WebView2 ランタイムが無い・起動できないとき。ノートの表示だけが使えないので、他の機能は使えるよう、ここに理由を出す
            ShowInitializationError(ex);
            return;
        }

        var core = WebView.CoreWebView2;

        // ページは表示だけ。ブラウザとしての機能は、すべて止める
        core.Settings.IsScriptEnabled = false;
        // 右クリックのメニューは、標準のものを出さず、カードの操作 (編集・削除・復元)と、選んだ文字のコピーだけに差し替える
        core.Settings.AreDefaultContextMenusEnabled = true;
        core.Settings.AreBrowserAcceleratorKeysEnabled = false;
        core.Settings.AreHostObjectsAllowed = false;
        core.Settings.IsWebMessageEnabled = false;
        core.Settings.IsStatusBarEnabled = false;
        core.Settings.IsZoomControlEnabled = false;
        core.Settings.IsGeneralAutofillEnabled = false;
        core.Settings.IsPasswordAutosaveEnabled = false;
#if !DEBUG
        core.Settings.AreDevToolsEnabled = false;
#endif

        // 全部のリクエストを受けて、仮想ホストのページだけを返す (それ以外は、失敗で返す)
        core.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
        core.WebResourceRequested += OnWebResourceRequested;
        core.NavigationStarting += OnNavigationStarting;
        core.NewWindowRequested += OnNewWindowRequested;
        core.NavigationCompleted += OnNavigationCompleted;
        core.ContextMenuRequested += OnContextMenuRequested;

        _webViewReady = true;
        if (_html.Length > 0)
        {
            core.Navigate(NoteLinks.PageUrl);
        }
    }

    /// <summary>WebView2 の初期化失敗の理由を表示する</summary>
    /// <param name="exception">初期化の失敗</param>
    private void ShowInitializationError(COMException exception)
    {
        WebView.Visibility = Visibility.Collapsed;
        InitializationErrorText.Text =
            $"ノートを表示できません (WebView2 を初期化できませんでした)。\nWebView2 ランタイムがインストールされているか確認してください。\n\n{exception.Message}";
        InitializationErrorText.Visibility = Visibility.Visible;
    }

    /// <summary>リクエストに答える (仮想ホストの <c>index.html</c> は、今のページ。それ以外は、失敗)</summary>
    /// <param name="sender">WebView2</param>
    /// <param name="args">リクエストの情報</param>
    private async void OnWebResourceRequested(CoreWebView2 sender, CoreWebView2WebResourceRequestedEventArgs args)
    {
        var deferral = args.GetDeferral();
        try
        {
            var isPage = Uri.TryCreate(args.Request.Uri, UriKind.Absolute, out var uri)
                && NoteLinks.IsPageUri(args.Request.Uri)
                && uri.AbsolutePath == "/index.html";

            var bytes = isPage ? Encoding.UTF8.GetBytes(_html) : [];
            var stream = new InMemoryRandomAccessStream();
            await stream.WriteAsync(bytes.AsBuffer());
            stream.Seek(0);

            args.Response = isPage
                ? sender.Environment.CreateWebResourceResponse(
                    stream, 200, "OK", $"Content-Type: text/html; charset=utf-8\r\nCache-Control: no-store\r\nContent-Security-Policy: {ContentSecurityPolicy}")
                : sender.Environment.CreateWebResourceResponse(stream, 403, "Blocked", "Cache-Control: no-store");
        }
        finally
        {
            deferral.Complete();
        }
    }

    /// <summary>ページの移動を横取りする (カードの操作と、本文のリンクだけを受け、それ以外は取り消す)</summary>
    /// <param name="sender">WebView2</param>
    /// <param name="args">移動の情報</param>
    private void OnNavigationStarting(CoreWebView2 sender, CoreWebView2NavigationStartingEventArgs args)
    {
        if (NoteLinks.TryParseAction(args.Uri, out var action, out var noteId))
        {
            args.Cancel = true;
            ActionRequested?.Invoke(this, new NoteActionEventArgs(action, noteId));
            return;
        }

        // 本文の file: のリンク (内部リンクに包んである)は、取り出して、ふつうのリンクと同じ判定に回す
        if (NoteLinks.TryParseOpen(args.Uri, out var wrapped))
        {
            args.Cancel = true;
            RequestLink(args.IsUserInitiated, wrapped.AbsoluteUri);
            return;
        }

        // アプリが開くページ (index.html)だけを通す
        if (args.Uri == NoteLinks.PageUrl)
        {
            return;
        }

        args.Cancel = true;
        RequestLink(args.IsUserInitiated, args.Uri);
    }

    /// <summary>新しいウィンドウで開く操作 (Ctrl+クリック・中クリック)を、ふつうのクリックと同じ判定に回す</summary>
    /// <param name="sender">WebView2</param>
    /// <param name="args">新しいウィンドウの情報</param>
    private void OnNewWindowRequested(CoreWebView2 sender, CoreWebView2NewWindowRequestedEventArgs args)
    {
        args.Handled = true;
        RequestLink(args.IsUserInitiated, NoteLinks.TryParseOpen(args.Uri, out var wrapped) ? wrapped.AbsoluteUri : args.Uri);
    }

    /// <summary>リンクを開く依頼を出す (利用者の操作で、今のページに出したリンクだけ)</summary>
    /// <param name="isUserInitiated">利用者の操作による移動か</param>
    /// <param name="uri">移動先の URL</param>
    private void RequestLink(bool isUserInitiated, string uri)
    {
        if (isUserInitiated
            && Uri.TryCreate(uri, UriKind.Absolute, out var link)
            && NoteLinks.IsOpenable(link)
            && _allowedLinks.Contains(link.AbsoluteUri))
        {
            LinkRequested?.Invoke(this, link);
        }
    }

    /// <summary>右クリックのメニューを、カードの操作に差し替える</summary>
    /// <param name="sender">WebView2</param>
    /// <param name="args">メニューの情報</param>
    /// <remarks>
    /// 右クリックした位置のカードは、アプリ側から実行するスクリプトで調べる (ページのスクリプトは止めてあるが、これは動く)。
    /// カードの上なら、「編集」「削除」(削除済みのカードなら「復元」)を出す。選んだ文字があるときは、「コピー」を出す。それ以外の項目は出さない。
    /// メニューは、WebView2 の標準のメニュー (項目の文字の幅しかなく、押しにくい)ではなく、アプリ (WinUI)の <see cref="MenuFlyout"/> で出す。
    /// </remarks>
    private async void OnContextMenuRequested(CoreWebView2 sender, CoreWebView2ContextMenuRequestedEventArgs args)
    {
        var deferral = args.GetDeferral();
        try
        {
            // WebView2 のメニューは、項目の文字の幅しかなく、押しにくい。アプリ (WinUI)のメニューを出す
            args.Handled = true;
            var hasSelection = args.ContextMenuTarget.HasSelection;
            var card = await FindCardAtAsync(sender, args.Location);

            var menu = new MenuFlyout();
            if (card is { } found)
            {
                var actions = found.IsDeleted
                    ? new[] { (NoteAction.Restore, "復元", Symbol.Undo) }
                    : [(NoteAction.Edit, "編集", Symbol.Edit), (NoteAction.Delete, "削除", Symbol.Delete)];
                foreach (var (action, label, symbol) in actions)
                {
                    var item = new MenuFlyoutItem { Text = label, Icon = new SymbolIcon(symbol), MinWidth = MenuMinWidth };
                    item.Click += (_, _) => ActionRequested?.Invoke(this, new NoteActionEventArgs(action, found.NoteId));
                    menu.Items.Add(item);
                }
            }
            if (hasSelection)
            {
                var copy = new MenuFlyoutItem { Text = "コピー", Icon = new SymbolIcon(Symbol.Copy), MinWidth = MenuMinWidth };
                copy.Click += async (_, _) => await CopySelectionAsync();
                menu.Items.Add(copy);
            }
            if (menu.Items.Count > 0)
            {
                menu.ShowAt(WebView, new FlyoutShowOptions { Position = args.Location });
            }
        }
        finally
        {
            deferral.Complete();
        }
    }

    /// <summary>ページで選んでいる文字を、クリップボードにコピーする</summary>
    /// <returns>コピーの完了を表すタスク</returns>
    private async Task CopySelectionAsync()
    {
        try
        {
            var text = JsonSerializer.Deserialize<string>(await WebView.CoreWebView2.ExecuteScriptAsync("window.getSelection().toString()"));
            if (!string.IsNullOrEmpty(text))
            {
                var package = new DataPackage();
                package.SetText(text);
                Clipboard.SetContent(package);
            }
        }
        catch (Exception ex) when (ex is COMException or JsonException or InvalidOperationException)
        {
            // コピーできなくても、表示には影響しない
        }
    }

    /// <summary>ページの、指定の位置にあるカードを調べる</summary>
    /// <param name="core">WebView2</param>
    /// <param name="location">ページの左上からの位置</param>
    /// <returns>カードのノートの番号と、削除済みか。カードの上でなければ null</returns>
    private static async Task<(int NoteId, bool IsDeleted)?> FindCardAtAsync(CoreWebView2 core, Windows.Foundation.Point location)
    {
        var x = location.X.ToString(CultureInfo.InvariantCulture);
        var y = location.Y.ToString(CultureInfo.InvariantCulture);
        var script =
            $"(function () {{ var e = document.elementFromPoint({x}, {y}); var a = e && e.closest('article.card'); return a ? a.id + (a.classList.contains('deleted') ? ':deleted' : '') : ''; }})()";
        try
        {
            var result = JsonSerializer.Deserialize<string>(await core.ExecuteScriptAsync(script));
            if (string.IsNullOrEmpty(result))
            {
                return null;
            }

            // 結果は "n" + ノートの番号。削除済みなら、末尾に ":deleted" が付く
            var isDeleted = result.EndsWith(":deleted", StringComparison.Ordinal);
            var id = isDeleted ? result[..^":deleted".Length] : result;
            return id.Length > 1 && int.TryParse(id.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out var noteId) ? (noteId, isDeleted) : null;
        }
        catch (Exception ex) when (ex is COMException or JsonException or InvalidOperationException)
        {
            // 調べられなかったときは、カードの操作を出さない (選んだ文字のコピーだけ残る)
            return null;
        }
    }

    /// <summary>ページの読み込みが終わったら、スクロールの位置を合わせる</summary>
    /// <param name="sender">WebView2</param>
    /// <param name="args">読み込みの情報</param>
    private async void OnNavigationCompleted(CoreWebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
    {
        _ready = args.IsSuccess;
        if (_ready && _pendingScroll > 0)
        {
            var position = _pendingScroll;
            _pendingScroll = 0;
            await sender.ExecuteScriptAsync($"window.scrollTo(0, {position.ToString(CultureInfo.InvariantCulture)})");
        }
    }
}
