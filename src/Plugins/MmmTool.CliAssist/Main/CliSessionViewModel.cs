using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using MmmSdk.Core.Components.Attachments;
using MmmSdk.Core.Components.Shells;
using MmmSdk.WinUI.Components.Attachments;
using MmmSdk.WinUI.Components.Errors;
using MmmSdk.WinUI.Components.Terminal;
using MmmTool.CliAssist.Core;

namespace MmmTool.CliAssist.Main;

/// <summary>CLI のセッション 1 つ分 (ターミナル・入力欄・添付)の ViewModel</summary>
/// <remarks>
/// 定型コマンド・環境は、セッションをまたいで共通なので、<see cref="CliAssistViewModel"/> が持つ。送信履歴はセッションごとに持つ。
/// セッションは <see cref="CliSessionFactory"/> が DI から作る (Transient)。
/// 添付の一時保存先もセッションごとに持つ (ほかのタブの送信で、送信前の添付が影響を受けないようにするため)。
/// タブを閉じるときは <see cref="Dispose"/> を呼ぶ (シェルが終了し、一時保存した添付が消える)。呼ばなくても、作ったスコープ (ページ)の破棄で同じ後始末が行われる。
/// </remarks>
public sealed partial class CliSessionViewModel : ObservableObject, IDisposable
{
    /// <summary>添付ファイルの一時保存先 (Windows の %TEMP%)</summary>
    private readonly AttachmentStore _windowsAttachmentStore;
    /// <summary>添付ファイルの一時保存先 (WSL の /tmp)</summary>
    private readonly AttachmentStore _wslAttachmentStore;
    /// <summary>画像の変換</summary>
    private readonly IImageConverter _imageConverter;
    /// <summary>送信履歴 (このセッション専用)</summary>
    private readonly SendHistory _sendHistory;
    /// <summary>時刻の取得元</summary>
    private readonly TimeProvider _timeProvider;
    /// <summary>UI スレッドの同期コンテキスト (作ったときのもの)</summary>
    private readonly SynchronizationContext? _uiContext;
    /// <summary>後始末を済ませたか</summary>
    private bool _disposed;

    /// <summary>セッションを作る</summary>
    /// <param name="terminal">ターミナルのセッション</param>
    /// <param name="windowsAttachmentStore">添付ファイルの一時保存先 (Windows の %TEMP%)</param>
    /// <param name="wslAttachmentStore">添付ファイルの一時保存先 (WSL の /tmp)</param>
    /// <param name="imageConverter">画像の変換</param>
    /// <param name="sendHistory">送信履歴 (セッションごとに 1 つ)</param>
    /// <param name="timeProvider">現在時刻の提供元</param>
    public CliSessionViewModel(
        ITerminalSession terminal,
        [FromKeyedServices(CliEnvironment.Windows)] AttachmentStore windowsAttachmentStore,
        [FromKeyedServices(CliEnvironment.Wsl)] AttachmentStore wslAttachmentStore,
        IImageConverter imageConverter,
        SendHistory sendHistory,
        TimeProvider timeProvider)
    {
        Terminal = terminal;
        _windowsAttachmentStore = windowsAttachmentStore;
        _wslAttachmentStore = wslAttachmentStore;
        _imageConverter = imageConverter;
        _sendHistory = sendHistory;
        _timeProvider = timeProvider;

        InputText = string.Empty;
        HistoryFilter = string.Empty;
        Title = string.Empty;

        Category = CommandCategory.Shell;
        _uiContext = SynchronizationContext.Current;
        Terminal.Exited += OnShellExited;

        Attachments.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasAttachments));
        HistoryItems.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasNoHistory));
    }

    /// <summary>左ペインで表示する定型コマンドの群 (シェル / AI セッション)</summary>
    /// <remarks>
    /// このタブのターミナルで、今シェルと AI セッションのどちらを動かしているかを表す。タブごとに持ち、タブを切り替えると左ペインが追従する。
    /// 新しいタブはシェルから始まる。
    /// </remarks>
    [ObservableProperty]
    public partial CommandCategory Category { get; set; }

    /// <summary>シェルが終了したときの処理 (中の AI セッションも無くなるので、シェル側へ戻す)</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">イベントの情報</param>
    /// <remarks>終了はスレッドプールで通知されるので、UI スレッドへ移して値を変える。</remarks>
    private void OnShellExited(object? sender, EventArgs e)
    {
        if (_uiContext is { } context)
        {
            context.Post(_ => Category = CommandCategory.Shell, null);
        }
    }

    /// <summary>シェルのセッション。</summary>
    /// <remarks>
    /// 起動するシェルは定型コマンドの環境 (<see cref="CliCommandSet.Environment"/>)で決まるので、<see cref="CliAssistViewModel.InitializeAsync"/> が済んでから画面につなぐ (つないだときに起動する)。
    /// 今どちらの環境で動いているかは、このシェルの種類 (<see cref="ShellInfo.Kind"/>)で見る (ここ 1 か所に持つ)。
    /// </remarks>
    public ITerminalSession Terminal { get; }

    /// <summary>シェルの作業ディレクトリ (Windows のパス。シェルの再起動時の開始位置でもある)</summary>
    /// <remarks>このアプリが送った作業ディレクトリ変更だけを反映する。プロンプトで直接打った <c>cd</c> は反映しない。</remarks>
    public string WorkingDirectory => Terminal.WorkingDirectory;

    /// <summary>タブの名前</summary>
    /// <remarks>名前は、ほかのタブとの兼ね合い (同じフォルダ名のタブがあるか)で決まるので、<see cref="CliAssistViewModel"/> が付ける。</remarks>
    [ObservableProperty]
    public partial string Title { get; set; }

    /// <summary>シェルの作業ディレクトリを決める (次に (再)起動するシェルの開始位置になる)</summary>
    /// <param name="directory">作業ディレクトリ (Windows のパス)</param>
    public void SetWorkingDirectory(string directory)
    {
        Terminal.WorkingDirectory = directory;
        OnPropertyChanged(nameof(WorkingDirectory));
    }

    /// <summary>今の環境で使う、添付ファイルの一時保存先</summary>
    private AttachmentStore AttachmentStore => Terminal.Shell.Kind == ShellKind.Wsl ? _wslAttachmentStore : _windowsAttachmentStore;

    #region View への依頼

    /// <summary>フォーカスの移動を View に頼む</summary>
    /// <remarks>ViewModel は UI に触れないため。</remarks>
    public event EventHandler<FocusTarget>? FocusRequested;

    /// <summary>ターミナルのシェルの起動し直しを View に頼む</summary>
    /// <remarks>起動し直すには端末の大きさが要り、それは画面 (ターミナルのコントロール)が持っているため。</remarks>
    public event EventHandler? TerminalRestartRequested;

    /// <summary>フォーカスを移すよう、View に頼む</summary>
    /// <param name="target">フォーカスを移す先</param>
    public void RequestFocus(FocusTarget target) => FocusRequested?.Invoke(this, target);

    /// <summary>ターミナルのシェルを起動し直すよう、View に頼む</summary>
    public void RequestTerminalRestart() => TerminalRestartRequested?.Invoke(this, EventArgs.Empty);

    #endregion

    #region 作業ディレクトリ

    /// <summary>アプリの EXE があるフォルダを、シェルから見たパスにする (<see cref="CommandPlaceholders.AppDir"/> の展開に使う)</summary>
    /// <returns>シェルから見たパス。WSL から開けない場所 (ネットワークのフォルダーなど)に置いたときは、Windows のパスのまま</returns>
    public string AppDirectoryForShell()
        => ShellCommands.TryConvertPath(Terminal.Shell, AppContext.BaseDirectory, out var path) ? path : AppContext.BaseDirectory;

    /// <summary>シェルの作業ディレクトリを変える</summary>
    /// <param name="directory">移動先のフォルダー</param>
    /// <returns>移動を始めたら true。シェルから開けないフォルダーのときは、画面に知らせて false</returns>
    /// <remarks>送れなかったとき (ターミナルが起動していない・シェルが終了している)も、これから (再)起動するシェルは、この場所から始める。</remarks>
    public bool TryChangeDirectory(string directory)
    {
        if (!ShellCommands.TryChangeDirectory(Terminal.Shell, directory, out var command))
        {
            Error.Show(Terminal.Shell.Kind == ShellKind.Wsl
                ? $"WSL からは開けないフォルダーです (ドライブ文字のあるフォルダーか、WSL のフォルダーを選んでください): {directory}"
                : $"cmd では、% を含むフォルダーへ移動できません (環境変数として展開されるため): {directory}");
            return false;
        }

        TrySubmit(command);
        // シェルを再起動したときも同じ場所から始める
        SetWorkingDirectory(directory);
        return true;
    }

    #endregion

    #region 送信

    /// <summary>下部の入力欄のテキスト。</summary>
    [ObservableProperty]
    public partial string InputText { get; set; }

    /// <summary>入力欄のテキスト (と添付ファイルの指示文・パス)をターミナルへ送る。</summary>
    /// <remarks>添付のパスは、シェルから見たパス (WSL では <c>/mnt/d/...</c>・<c>/tmp/...</c>)にして送る。変換できるかは、添付したときに確かめてある。</remarks>
    [RelayCommand]
    private void Send()
    {
        var text = InputText;
        var attachmentPaths = Attachments.Select(item => ToShellPath(item.FilePath)).ToList();
        if (string.IsNullOrWhiteSpace(text) && attachmentPaths.Count == 0)
        {
            return;
        }

        if (!TrySubmit(SendText.Compose(text, attachmentPaths)))
        {
            // 入力欄と添付は残す (送れるようになってから、もう一度送れるように)
            return;
        }

        InputText = string.Empty;
        Attachments.Clear();
        AttachmentStore.CloseSession();

        // 履歴には入力欄の本文だけを残す (自動で付け足した指示文・パスは残さない)
        AddSendHistory(text.TrimEnd('\r', '\n'));
    }

    /// <summary>ターミナルが使える状態なら、テキストを送る (使えなければ、画面に知らせる)</summary>
    /// <param name="text">送るテキスト</param>
    /// <returns>送ったら true。ターミナルが起動していない・シェルが終了しているときは false</returns>
    /// <remarks>
    /// 起動していないのは、起動の直前・再起動の途中のほか、WebView2 を初期化できなかったとき (ターミナルの場所に理由が出る)。
    /// 黙って捨てると、押したのに何も起きない状態になるので、知らせる。
    /// </remarks>
    public bool TrySubmit(string text)
    {
        if (!Terminal.IsStarted)
        {
            Error.Show("ターミナルが起動していないため、送信できませんでした。WebView2 を使えない環境では、ターミナルは使えません。");
            return false;
        }
        if (Terminal.HasExited)
        {
            Error.Show("シェルが終了しています。ターミナルで何かキーを押して再起動してから、もう一度送信してください。");
            return false;
        }

        Terminal.Submit(text);
        return true;
    }

    #endregion

    #region 添付

    /// <summary>添付ファイルの一覧</summary>
    public ObservableCollection<AttachmentItem> Attachments { get; } = [];

    /// <summary>添付があるか</summary>
    public bool HasAttachments => Attachments.Count > 0;

    /// <summary>ディスク上のファイルを添付する (ドラッグ＆ドロップ・ファイルの貼り付け)。</summary>
    /// <param name="filePath">添付するファイルのパス</param>
    /// <remarks>
    /// コピーせず、元のパスをそのまま送る (ローカルで動く CLI は、元の場所のファイルを直接読めるため)。
    /// WSL では、WSL から開けない場所 (ネットワークのフォルダーなど)のファイルは添付せず、知らせる (送ってから読めないと分かるより、先に分かるほうがよいため)。
    /// </remarks>
    public void AddAttachmentFile(string filePath)
    {
        if (!ShellCommands.TryConvertPath(Terminal.Shell, filePath, out _))
        {
            Error.Show($"WSL からは開けない場所のファイルなので、添付できません (ドライブ文字のある場所か、WSL のフォルダーに置いてください): {filePath}");
            return;
        }
        Attachments.Add(new AttachmentItem(filePath, Path.GetFileName(filePath), isTemporary: false));
    }

    /// <summary>添付ファイルのパスを、シェルから見たパスにする</summary>
    /// <param name="filePath">添付ファイルのパス (Windows のパス)</param>
    /// <returns>シェルから見たパス</returns>
    /// <exception cref="InvalidOperationException">変換できない (添付したときに確かめてあるので、起きればバグ)。</exception>
    private string ToShellPath(string filePath)
        => ShellCommands.TryConvertPath(Terminal.Shell, filePath, out var path)
            ? path
            : throw new InvalidOperationException($"添付ファイルのパスを、シェルから見たパスにできません: {filePath}");

    /// <summary>ディスク上に無いファイルを一時保存して添付する (メールの添付ファイルなど、パスの無いファイルのドロップ・貼り付け)。</summary>
    /// <param name="content">ファイルの内容のストリーム</param>
    /// <param name="fileName">ファイル名</param>
    /// <returns>添付の完了を表すタスク</returns>
    public async Task AddAttachmentContentAsync(Stream content, string fileName)
    {
        try
        {
            using var buffer = new MemoryStream();
            await content.CopyToAsync(buffer);
            var savedPath = await AttachmentStore.AddAsync(buffer.ToArray(), fileName);
            Attachments.Add(new AttachmentItem(savedPath, fileName, isTemporary: true));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Error.Show($"{fileName} を添付できませんでした。{ex.Message}");
        }
    }

    /// <summary>画像を JPEG にして添付する (画像の貼り付け)。</summary>
    /// <param name="image">添付する画像のストリーム</param>
    /// <returns>添付の完了を表すタスク</returns>
    public async Task AddAttachmentImageAsync(Stream image)
    {
        try
        {
            var jpeg = await _imageConverter.ToJpegAsync(image);
            var savedPath = await AttachmentStore.AddAsync(jpeg, "clipboard.jpg");
            Attachments.Add(new AttachmentItem(savedPath, "貼り付けた画像", isTemporary: true));
        }
        catch (Exception ex) when (ex is COMException or IOException or UnauthorizedAccessException)
        {
            // COMException: 画像のデコード・変換の失敗 (未対応の形式など)
            Error.Show($"画像を添付できませんでした。{ex.Message}");
        }
    }

    /// <summary>添付をすべて取り除く</summary>
    /// <remarks>環境が変わるとき、今の環境の一時保存先から消すために、環境を切り替える前に呼ぶ。</remarks>
    public void RemoveAllAttachments()
    {
        foreach (var item in Attachments.ToList())
        {
            RemoveAttachment(item);
        }
    }

    /// <summary>添付を取り除く</summary>
    /// <param name="item">取り除く添付</param>
    /// <remarks>一時保存したファイルだけを削除する。元の場所のファイルは一覧から外すだけで、決して消さない。</remarks>
    [RelayCommand]
    private void RemoveAttachment(AttachmentItem item)
    {
        Attachments.Remove(item);
        if (!item.IsTemporary)
        {
            return;
        }

        try
        {
            AttachmentStore.Remove(item.FilePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Error.Show($"添付ファイルを削除できませんでした。{ex.Message}");
        }
    }

    #endregion

    #region 送信履歴

    /// <summary>送信履歴の一覧 (絞り込み後)</summary>
    public ObservableCollection<SendHistoryItem> HistoryItems { get; } = [];

    /// <summary>表示する履歴が無いか</summary>
    public bool HasNoHistory => HistoryItems.Count == 0;

    /// <summary>履歴の絞り込み文字列。</summary>
    [ObservableProperty]
    public partial string HistoryFilter { get; set; }

    /// <summary>絞り込み文字列が変わったら、一覧を作り直す</summary>
    /// <param name="value">変更後の絞り込み文字列</param>
    partial void OnHistoryFilterChanged(string value) => RefreshHistory();

    /// <summary>履歴の一覧を開く前に、最新の内容にする。</summary>
    public void PrepareHistory()
    {
        HistoryFilter = string.Empty;
        RefreshHistory();
    }

    /// <summary>履歴の本文を入力欄へ戻す。</summary>
    /// <param name="item">戻す履歴の行</param>
    public void RestoreHistory(SendHistoryItem item) => InputText = item.Text;

    /// <summary>送信した本文を履歴の先頭に追加する</summary>
    /// <param name="text">送信した本文</param>
    /// <remarks>空白のみは無視し、同じ本文は先頭へ移す。</remarks>
    private void AddSendHistory(string text) => _sendHistory.Add(text, _timeProvider.GetLocalNow());

    /// <summary>絞り込み文字列に合わせて、履歴の一覧を作り直す</summary>
    private void RefreshHistory()
    {
        var today = _timeProvider.GetLocalNow().Date;

        HistoryItems.Clear();
        foreach (var entry in _sendHistory.Search(HistoryFilter))
        {
            var sentAt = entry.SentAt.ToLocalTime();
            var sentAtText = sentAt.Date == today ? sentAt.ToString("HH:mm") : sentAt.ToString("M/d HH:mm");
            HistoryItems.Add(new SendHistoryItem(entry.Text, SendText.ToSingleLine(entry.Text), sentAtText));
        }
    }

    #endregion

    /// <summary>タブを閉じる後始末 (シェルを終了し、一時保存した添付を消す)</summary>
    /// <remarks>画面 (<see cref="CliSessionView"/>)からターミナルを外してから呼ぶ。何度呼んでもよい。</remarks>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;

        Terminal.Exited -= OnShellExited;
        Terminal.Dispose();
        _windowsAttachmentStore.Dispose();
        _wslAttachmentStore.Dispose();
    }

    #region エラー表示

    /// <summary>画面に出すエラー</summary>
    public ErrorState Error { get; } = new();

    #endregion
}
