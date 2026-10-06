using System.Collections.Specialized;
using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MmmSdk.Core.Components.Hosting;
using MmmSdk.WinUI.Components.Pages;
using MmmSdk.WinUI.Utilities;
using MmmTool.CliAssist.Core;

namespace MmmTool.CliAssist.Main;

/// <summary>CLI補助ページ</summary>
public sealed partial class CliAssistPage : Page, IReleasablePage
{
    /// <summary>ページの ViewModel</summary>
    public CliAssistViewModel ViewModel { get; }

    /// <summary>セッションごとのタブと画面</summary>
    private readonly Dictionary<CliSessionViewModel, SessionTab> _tabs = [];
    /// <summary>WebView2 のデータ (キャッシュなど)の保存先フォルダー</summary>
    /// <remarks>データのフォルダーの下の <c>WebView2</c>(WebView2 の既定の EXE の横ではなく、データにまとめる)。</remarks>
    private readonly string _webView2Directory;
    /// <summary>ターミナルをつないでよいか (定型コマンドの読み込みが済んだか)</summary>
    private bool _terminalsConnectable;

    /// <summary>ページを作る</summary>
    /// <param name="viewModel">ページの ViewModel</param>
    /// <param name="environment">アプリの名前と、データ・アイコンの置き場所</param>
    public CliAssistPage(CliAssistViewModel viewModel, AppEnvironment environment)
    {
        ViewModel = viewModel;
        InitializeComponent();

        _webView2Directory = Path.Combine(environment.DataDirectory, "WebView2");
        foreach (var session in ViewModel.Sessions)
        {
            AddTab(session);
        }
        ViewModel.Sessions.CollectionChanged += OnSessionsChanged;
        SessionTabs.SelectedItem = _tabs[ViewModel.SelectedSession].Item;
        ShowSelectedSession();
        UpdateClosable();

        ViewModel.PropertyChanged += (_, e) =>
        {
            switch (e.PropertyName)
            {
                case nameof(CliAssistViewModel.CommandItems):
                    RebuildCommandTree();
                    break;
                case nameof(CliAssistViewModel.SelectedSession):
                    // 閉じる・開くなどで選ばれたセッションが変わったら、タブも合わせる
                    SessionTabs.SelectedItem = _tabs[ViewModel.SelectedSession].Item;
                    ShowSelectedSession();
                    break;
                case nameof(CliAssistViewModel.SelectedCategory):
                    // コマンドの実行などでタブが切り替わったら、タブの見た目も合わせる
                    CategorySelector.SelectedItem = ViewModel.SelectedCategory == CommandCategory.Session
                        ? SessionCategoryItem
                        : ShellCategoryItem;
                    break;
            }
        };
        RebuildCommandTree();
    }

    /// <inheritdoc />
    /// <remarks>機能をオフにしたとき。ターミナルのコントロールからセッションを外す。シェル (とその中の CLI)は、このあとのスコープの破棄で終了する。</remarks>
    public void Release()
    {
        foreach (var tab in _tabs.Values)
        {
            tab.View.Release();
        }
    }

    /// <summary>読み込み時の処理 (ViewModel の初期化・ターミナルの接続)</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">イベントの情報</param>
    /// <remarks>
    /// 起動するシェル (Windows / WSL)は、初期化で読み込む定型コマンドで決まるので、初期化が済んでからターミナルにつなぐ。
    /// ターミナルは、つないだとき (表示の準備がまだなら、準備ができたとき)にシェルを起動する。
    /// </remarks>
    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await ViewModel.InitializeAsync();
        _terminalsConnectable = true;
        foreach (var tab in _tabs.Values)
        {
            tab.View.ConnectTerminal();
        }
    }

    #region セッション (タブ)

    /// <summary>セッション 1 つ分のタブと、その中の画面</summary>
    /// <param name="Item">タブ</param>
    /// <param name="View">タブの中の画面 (ターミナルと送信欄)</param>
    /// <param name="OnSessionChanged">セッションの変更を受けるハンドラ (タブを取り除くときに外す)</param>
    private sealed record SessionTab(TabViewItem Item, CliSessionView View, PropertyChangedEventHandler OnSessionChanged);

    /// <summary>選ばれているセッションの画面を、タブの下の枠に入れる</summary>
    /// <remarks>
    /// 選ばれていないタブの画面は、枠から外れる。ターミナルは外れても作り直されない (再表示のときは、フォーカスを戻すだけ)。
    /// </remarks>
    private void ShowSelectedSession()
        => SessionHost.Child = _tabs.TryGetValue(ViewModel.SelectedSession, out var tab) ? tab.View : null;

    /// <summary>セッションの一覧が変わったら、タブを合わせる</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">変更の情報</param>
    private void OnSessionsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (var session in e.NewItems?.Cast<CliSessionViewModel>() ?? [])
        {
            AddTab(session);
        }
        foreach (var session in e.OldItems?.Cast<CliSessionViewModel>() ?? [])
        {
            RemoveTab(session);
        }

        UpdateClosable();
    }

    /// <summary>「×」を出すかを合わせる</summary>
    /// <remarks>最後の 1 つは閉じられない (ターミナルが 1 つも無い画面は意味が無いため)ので、「×」を出さない。</remarks>
    private void UpdateClosable()
    {
        foreach (var tab in _tabs.Values)
        {
            tab.Item.IsClosable = ViewModel.Sessions.Count > 1;
        }
    }

    /// <summary>セッションのタブを作って、並べる</summary>
    /// <param name="session">セッション</param>
    /// <remarks>
    /// 定型コマンドの読み込みが済んでいれば、ターミナルをつなぐ (つないだとき、シェルが起動する)。済んでいなければ、読み込みのあとでつなぐ。
    /// 選ばれていないタブは画面から外れるが、ターミナルは外れても作り直されない (再表示のときは、フォーカスを戻すだけ)。
    /// </remarks>
    private void AddTab(CliSessionViewModel session)
    {
        var view = new CliSessionView { ViewModel = session, WebView2Directory = _webView2Directory };
        var header = new TextBlock { Text = session.Title, TextTrimming = TextTrimming.CharacterEllipsis };
        var item = new TabViewItem { Header = header, Tag = session };
        ToolTipService.SetToolTip(item, session.WorkingDirectory);

        // タブ名 (フォルダ名)と、ツールチップ (パス全体)を、セッションに合わせる
        PropertyChangedEventHandler onSessionChanged = (_, e) =>
        {
            switch (e.PropertyName)
            {
                case nameof(CliSessionViewModel.Title):
                    header.Text = session.Title;
                    break;
                case nameof(CliSessionViewModel.WorkingDirectory):
                    ToolTipService.SetToolTip(item, session.WorkingDirectory);
                    break;
            }
        };
        session.PropertyChanged += onSessionChanged;

        _tabs[session] = new SessionTab(item, view, onSessionChanged);
        SessionTabs.TabItems.Add(item);
        if (_terminalsConnectable)
        {
            view.ConnectTerminal();
        }
    }

    /// <summary>セッションのタブを取り除く</summary>
    /// <param name="session">取り除くセッション</param>
    /// <remarks>
    /// ターミナルを画面から外し、セッションとのつなぎも外す。シェルの終了は、このあと ViewModel が行う。
    /// つなぎを外すのは、閉じたセッションが DI のスコープに (ページを捨てるまで)残るため、画面 (WebView2 を含む)まで残さないため。
    /// </remarks>
    private void RemoveTab(CliSessionViewModel session)
    {
        if (!_tabs.Remove(session, out var tab))
        {
            return;
        }

        session.PropertyChanged -= tab.OnSessionChanged;
        if (SessionHost.Child == tab.View)
        {
            SessionHost.Child = null;
        }
        tab.View.Release();
        SessionTabs.TabItems.Remove(tab.Item);
    }

    /// <summary>タブが選ばれたら、ViewModel に反映する</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">選択の変更の情報</param>
    private void OnSessionTabSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SessionTabs.SelectedItem is TabViewItem { Tag: CliSessionViewModel session } && ViewModel.Sessions.Contains(session))
        {
            ViewModel.SelectedSession = session;
        }
    }

    /// <summary>「＋」が押されたら、新しいタブを開く</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="args">イベントの情報</param>
    private void OnAddTabButtonClick(TabView sender, object args) => ViewModel.AddSessionCommand.Execute(null);

    /// <summary>タブの「×」が押されたら、確認してから閉じる</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="args">閉じるタブの情報</param>
    private void OnTabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
    {
        if (args.Tab.Tag is CliSessionViewModel session)
        {
            ViewModel.CloseSessionCommand.Execute(session);
        }
    }

    #endregion

    #region 定型コマンド

    /// <summary>タブが選ばれたら、ViewModel に反映する</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="args">選択の変更の情報</param>
    private void OnCategorySelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
        => ViewModel.SelectedCategory = sender.SelectedItem == SessionCategoryItem ? CommandCategory.Session : CommandCategory.Shell;

    /// <summary>ツリーのノードを作り直す</summary>
    /// <remarks>すべてのノードを最初から展開した状態にする。</remarks>
    private void RebuildCommandTree()
    {
        CommandTree.RootNodes.Clear();
        foreach (var item in ViewModel.CommandItems)
        {
            CommandTree.RootNodes.Add(CreateNode(item));
        }
    }

    /// <summary>ViewModel の要素から、ツリーのノードを作る (子も展開した状態)</summary>
    /// <param name="item">ツリーの要素</param>
    /// <returns>ツリーのノード</returns>
    private static TreeViewNode CreateNode(CommandTreeItem item)
    {
        var node = new TreeViewNode { Content = item, IsExpanded = true };
        foreach (var child in item.Children)
        {
            node.Children.Add(CreateNode(child));
        }
        return node;
    }

    /// <summary>ツリーを横にもスクロールできるようにする。</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">イベントの情報</param>
    /// <remarks>TreeView は既定で横スクロールが無効 (長い名前は右が切れる)。テンプレート内の ScrollViewer に直接設定する。</remarks>
    private void OnCommandTreeLoaded(object sender, RoutedEventArgs e)
    {
        if (VisualTreeSearch.FindDescendant<ScrollViewer>(CommandTree) is { } scrollViewer)
        {
            scrollViewer.HorizontalScrollMode = ScrollMode.Auto;
            scrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
        }
    }

    /// <summary>ツリーの項目が選ばれたら、コマンドを実行する</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="args">選ばれた項目の情報</param>
    private void OnCommandTreeItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
    {
        // 手作りのノードでは、InvokedItem はノード自身で、データはその Content にある
        var item = args.InvokedItem is TreeViewNode node ? node.Content : args.InvokedItem;

        // 中間ノードは開閉だけ (TreeView が行う)
        if (item is CommandTreeItem { Kind: not CommandItemKind.Group } commandItem)
        {
            ViewModel.InvokeCommandItemCommand.Execute(commandItem);
        }
    }

    #endregion
}
