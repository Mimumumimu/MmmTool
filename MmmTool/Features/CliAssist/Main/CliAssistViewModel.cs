using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using MmmSdk.Core.Components.Attachments;
using MmmSdk.Core.Components.Shells;
using MmmSdk.Core.Components.Storage;
using MmmSdk.WinUI.Components.Attachments;
using MmmSdk.WinUI.Components.Errors;
using MmmSdk.WinUI.Components.Terminal;
using MmmTool.Core.CliAssist;
using MmmTool.Features.CliAssist.Setup;
using MmmTool.Features.CliAssist.WorkingDirectory;

namespace MmmTool.Features.CliAssist.Main;

/// <summary>CLI補助ページの ViewModel</summary>
public sealed partial class CliAssistViewModel : ObservableObject
{
    /// <summary>定型コマンドの保存先</summary>
    private readonly ICliCommandRepository _commandRepository;
    /// <summary>CLI補助の利用状態</summary>
    private readonly CliSettingsService _settings;
    /// <summary>添付ファイルの一時保存先（Windows の %TEMP%）</summary>
    private readonly AttachmentStore _windowsAttachmentStore;
    /// <summary>添付ファイルの一時保存先（WSL の /tmp）</summary>
    private readonly AttachmentStore _wslAttachmentStore;
    /// <summary>画像の変換</summary>
    private readonly IImageConverter _imageConverter;
    /// <summary>作業ディレクトリ変更ダイアログ</summary>
    private readonly IWorkingDirectoryDialogService _workingDirectoryDialog;
    /// <summary>初期設定ダイアログ</summary>
    private readonly ICliSetupDialogService _setupDialog;
    /// <summary>時刻の取得元</summary>
    private readonly TimeProvider _timeProvider;

    /// <summary>読み込んだ定型コマンド</summary>
    private CliCommandSet _commandSet = new();
    /// <summary>初期化済みか</summary>
    private bool _initialized;

    /// <summary>ViewModel を作る</summary>
    /// <param name="terminal">ターミナルのセッション</param>
    /// <param name="commandRepository">定型コマンドの保存先</param>
    /// <param name="settings">CLI補助の利用状態</param>
    /// <param name="windowsAttachmentStore">添付ファイルの一時保存先（Windows の %TEMP%）</param>
    /// <param name="wslAttachmentStore">添付ファイルの一時保存先（WSL の /tmp）</param>
    /// <param name="imageConverter">画像の変換</param>
    /// <param name="workingDirectoryDialog">作業ディレクトリ変更ダイアログを開く</param>
    /// <param name="setupDialog">初期設定ダイアログを開く</param>
    /// <param name="timeProvider">現在時刻の提供元</param>
    public CliAssistViewModel(
        ITerminalSession terminal,
        ICliCommandRepository commandRepository,
        CliSettingsService settings,
        [FromKeyedServices(CliEnvironment.Windows)] AttachmentStore windowsAttachmentStore,
        [FromKeyedServices(CliEnvironment.Wsl)] AttachmentStore wslAttachmentStore,
        IImageConverter imageConverter,
        IWorkingDirectoryDialogService workingDirectoryDialog,
        ICliSetupDialogService setupDialog,
        TimeProvider timeProvider)
    {
        Terminal = terminal;
        _commandRepository = commandRepository;
        _settings = settings;
        _windowsAttachmentStore = windowsAttachmentStore;
        _wslAttachmentStore = wslAttachmentStore;
        _imageConverter = imageConverter;
        _workingDirectoryDialog = workingDirectoryDialog;
        _setupDialog = setupDialog;
        _timeProvider = timeProvider;

        CommandItems = [];
        InputText = string.Empty;
        HistoryFilter = string.Empty;

        // 前回の作業ディレクトリでシェルを始める
        if (_settings.StartDirectory is { } startDirectory)
        {
            Terminal.WorkingDirectory = startDirectory;
        }

        Attachments.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasAttachments));
        HistoryItems.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasNoHistory));
    }

    /// <summary>中央のターミナルで動くシェルのセッション。</summary>
    /// <remarks>
    /// 起動するシェルは定型コマンドの環境（<see cref="CliCommandSet.Environment"/>）で決まるので、<see cref="InitializeAsync"/> が済んでから画面につなぐ（つないだときに起動する）。
    /// 今どちらの環境で動いているかは、このシェルの種類（<see cref="ShellInfo.Kind"/>）で見る（ここ 1 か所に持つ）。
    /// </remarks>
    public ITerminalSession Terminal { get; }

    /// <summary>今の環境で使う、添付ファイルの一時保存先</summary>
    private AttachmentStore AttachmentStore => Terminal.Shell.Kind == ShellKind.Wsl ? _wslAttachmentStore : _windowsAttachmentStore;

    #region 定型コマンド

    /// <summary>左ペインで表示中のコマンド群（シェル / AI セッション）。</summary>
    [ObservableProperty]
    public partial CommandCategory SelectedCategory { get; set; }

    /// <summary>フォーカスの移動を View に頼む</summary>
    /// <remarks>ViewModel は UI に触れないため。</remarks>
    public event EventHandler<FocusTarget>? FocusRequested;

    /// <summary>ターミナルのシェルの起動し直しを View に頼む</summary>
    /// <remarks>起動し直すには端末の大きさが要り、それは画面（ターミナルのコントロール）が持っているため。</remarks>
    public event EventHandler? TerminalRestartRequested;

    /// <summary>左ペインのツリーに表示する要素。</summary>
    [ObservableProperty]
    public partial IReadOnlyList<CommandTreeItem> CommandItems { get; private set; }

    /// <summary>定型コマンドを読み込んで表示を初期化する</summary>
    /// <returns>初期化の完了を表すタスク</returns>
    /// <remarks>
    /// 最初の 1 回だけ行う。定型コマンドのファイルが無いときは、読み込みの中で、環境を選ぶダイアログが開く。
    /// 読み込んだ環境に合わせて、ターミナルで起動するシェルを決める。読み込めなかったときは Windows のまま。
    /// </remarks>
    public async Task InitializeAsync()
    {
        if (_initialized)
        {
            return;
        }
        _initialized = true;

        // 読み込みの問題は複数あり得るので、まとめて 1 つの InfoBar で知らせる
        List<string?> messages = [_settings.LoadError, _settings.RecoveryMessage];
        try
        {
            string? recoveryMessage;
            (_commandSet, recoveryMessage) = await _commandRepository.LoadAsync();
            messages.Add(recoveryMessage);
            messages.AddRange(_commandSet.Validate());
            Terminal.Shell = ShellFor(_commandSet);
        }
        catch (DataFileException ex)
        {
            messages.Add($"{ex.Message}\n定型コマンドは表示されません。");
        }

        if (messages.OfType<string>().ToList() is { Count: > 0 } errors)
        {
            Error.Show(string.Join("\n\n", errors));
        }
        RebuildCommandItems();
    }

    /// <summary>定型コマンドを既定の内容に作り直す（初期化）</summary>
    /// <returns>初期化の完了を表すタスク</returns>
    /// <remarks>
    /// 初期設定ダイアログ（警告を出して、確認を兼ねる）で、使うツールと環境を選び直し、その既定の内容で上書きする（今の内容は引き継がない）。
    /// ターミナルは、環境が変わらなくても、いつも起動し直す（ユーザーの決定）。動いている CLI は終了する。
    /// 環境が変わったときは、添付を外す（パスの形・一時保存先が環境ごとに違うため）。一時保存した添付は消す。
    /// </remarks>
    [RelayCommand]
    private async Task ResetCommandsAsync()
    {
        if (await _setupDialog.ShowResetAsync(_commandSet.GetEnvironment()) is not { } setup)
        {
            return;
        }

        var commandSet = CliCommandDefaults.Create(setup);
        try
        {
            await _commandRepository.SaveAsync(commandSet);
        }
        catch (DataFileException ex)
        {
            Error.Show(ex.Message);
            return;
        }
        _commandSet = commandSet;

        var shell = ShellFor(_commandSet);
        if (shell.Kind != Terminal.Shell.Kind)
        {
            RemoveAllAttachments();
        }
        Terminal.Shell = shell;

        SelectedCategory = CommandCategory.Shell;
        RebuildCommandItems();
        TerminalRestartRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>定型コマンドの環境で起動するシェルを決める</summary>
    /// <param name="commandSet">定型コマンド</param>
    /// <returns>WSL なら WSL のシェル、そうでなければ既定のシェル</returns>
    private static ShellInfo ShellFor(CliCommandSet commandSet)
        => commandSet.GetEnvironment() == CliEnvironment.Wsl ? ShellLocator.Wsl : ShellLocator.Default;

    /// <summary>タブが切り替わったら、ツリーを作り直す</summary>
    /// <param name="value">切り替え後のタブ</param>
    partial void OnSelectedCategoryChanged(CommandCategory value) => RebuildCommandItems();

    /// <summary>選択中のタブの定型コマンドで、ツリーを作り直す</summary>
    private void RebuildCommandItems()
    {
        var items = new List<CommandTreeItem>();
        var nodes = SelectedCategory == CommandCategory.Shell ? _commandSet.Shell : _commandSet.Session;

        // 「作業ディレクトリ変更」はシェル側の先頭に固定で置く（定義ファイルには含めない）
        if (SelectedCategory == CommandCategory.Shell)
        {
            items.Add(CommandTreeItem.ChangeDirectory());
        }
        items.AddRange(nodes.Select(CommandTreeItem.From));
        CommandItems = items;
    }

    /// <summary>ツリーの要素を実行する（コマンドなら送信、作業ディレクトリ変更ならダイアログ）。</summary>
    /// <param name="item">実行するツリーの要素</param>
    /// <returns>実行の完了を表すタスク</returns>
    [RelayCommand]
    private async Task InvokeCommandItemAsync(CommandTreeItem item)
    {
        switch (item.Kind)
        {
            case CommandItemKind.Command when item.Command is { } command:
                if (!TrySubmit(CommandPlaceholders.Expand(command, AppDirectoryForShell())))
                {
                    break;
                }
                if (item.SwitchTo is { } tab)
                {
                    SelectedCategory = tab;
                }
                if (item.Focus is { } focus)
                {
                    FocusRequested?.Invoke(this, focus);
                }
                break;
            case CommandItemKind.ChangeDirectory:
                await ChangeWorkingDirectoryAsync();
                break;
        }
    }

    /// <summary>アプリの EXE があるフォルダを、シェルから見たパスにする（<see cref="CommandPlaceholders.AppDir"/> の展開に使う）</summary>
    /// <returns>シェルから見たパス。WSL から開けない場所（ネットワークのフォルダーなど）に置いたときは、Windows のパスのまま</returns>
    private string AppDirectoryForShell()
        => ShellCommands.TryConvertPath(Terminal.Shell, AppContext.BaseDirectory, out var path) ? path : AppContext.BaseDirectory;

    /// <summary>作業ディレクトリ変更ダイアログを開き、選ばれたフォルダへ移動する</summary>
    /// <returns>作業ディレクトリの変更の完了を表すタスク</returns>
    private async Task ChangeWorkingDirectoryAsync()
    {
        if (await _workingDirectoryDialog.ShowAsync() is not { } directory)
        {
            return;
        }

        if (!ShellCommands.TryChangeDirectory(Terminal.Shell, directory, out var command))
        {
            Error.Show(Terminal.Shell.Kind == ShellKind.Wsl
                ? $"WSL からは開けないフォルダーです（ドライブ文字のあるフォルダーか、WSL のフォルダーを選んでください）: {directory}"
                : $"cmd では、% を含むフォルダーへ移動できません（環境変数として展開されるため）: {directory}");
            return;
        }

        // 送れなかったときも、これから（再）起動するシェルは、この場所から始める
        TrySubmit(command);
        // シェルを再起動したときも同じ場所から始める
        Terminal.WorkingDirectory = directory;

        try
        {
            await _settings.AddDirectoryAsync(directory);
        }
        catch (DataFileException ex)
        {
            Error.Show(ex.Message);
        }
    }

    #endregion

    #region 送信

    /// <summary>下部の入力欄のテキスト。</summary>
    [ObservableProperty]
    public partial string InputText { get; set; }

    /// <summary>入力欄のテキスト（と添付ファイルの指示文・パス）をターミナルへ送る。</summary>
    /// <remarks>添付のパスは、シェルから見たパス（WSL では <c>/mnt/d/...</c>・<c>/tmp/...</c>）にして送る。変換できるかは、添付したときに確かめてある。</remarks>
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
            // 入力欄と添付は残す（送れるようになってから、もう一度送れるように）
            return;
        }

        InputText = string.Empty;
        Attachments.Clear();
        AttachmentStore.CloseSession();

        // 履歴には入力欄の本文だけを残す（自動で付け足した指示文・パスは残さない）
        AddSendHistory(text.TrimEnd('\r', '\n'));
    }

    /// <summary>ターミナルが使える状態なら、テキストを送る（使えなければ、画面に知らせる）</summary>
    /// <param name="text">送るテキスト</param>
    /// <returns>送ったら true。ターミナルが起動していない・シェルが終了しているときは false</returns>
    /// <remarks>
    /// 起動していないのは、起動の直前・再起動の途中のほか、WebView2 を初期化できなかったとき（ターミナルの場所に理由が出る）。
    /// 黙って捨てると、押したのに何も起きない状態になるので、知らせる。
    /// </remarks>
    private bool TrySubmit(string text)
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

    /// <summary>ディスク上のファイルを添付する（ドラッグ＆ドロップ・ファイルの貼り付け）。</summary>
    /// <param name="filePath">添付するファイルのパス</param>
    /// <remarks>
    /// コピーせず、元のパスをそのまま送る（ローカルで動く CLI は、元の場所のファイルを直接読めるため）。
    /// WSL では、WSL から開けない場所（ネットワークのフォルダーなど）のファイルは添付せず、知らせる（送ってから読めないと分かるより、先に分かるほうがよいため）。
    /// </remarks>
    public void AddAttachmentFile(string filePath)
    {
        if (!ShellCommands.TryConvertPath(Terminal.Shell, filePath, out _))
        {
            Error.Show($"WSL からは開けない場所のファイルなので、添付できません（ドライブ文字のある場所か、WSL のフォルダーに置いてください）: {filePath}");
            return;
        }
        Attachments.Add(new AttachmentItem(filePath, Path.GetFileName(filePath), isTemporary: false));
    }

    /// <summary>添付ファイルのパスを、シェルから見たパスにする</summary>
    /// <param name="filePath">添付ファイルのパス（Windows のパス）</param>
    /// <returns>シェルから見たパス</returns>
    /// <exception cref="InvalidOperationException">変換できない（添付したときに確かめてあるので、起きればバグ）。</exception>
    private string ToShellPath(string filePath)
        => ShellCommands.TryConvertPath(Terminal.Shell, filePath, out var path)
            ? path
            : throw new InvalidOperationException($"添付ファイルのパスを、シェルから見たパスにできません: {filePath}");

    /// <summary>ディスク上に無いファイルを一時保存して添付する（メールの添付ファイルなど、パスの無いファイルのドロップ・貼り付け）。</summary>
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

    /// <summary>画像を JPEG にして添付する（画像の貼り付け）。</summary>
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
            // COMException: 画像のデコード・変換の失敗（未対応の形式など）
            Error.Show($"画像を添付できませんでした。{ex.Message}");
        }
    }

    /// <summary>添付をすべて取り除く</summary>
    /// <remarks>環境が変わるとき、今の環境の一時保存先から消すために、環境を切り替える前に呼ぶ。</remarks>
    private void RemoveAllAttachments()
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

    /// <summary>送信履歴</summary>
    private readonly SendHistory _sendHistory = new();

    /// <summary>送信履歴の一覧（絞り込み後）</summary>
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

    #region エラー表示

    /// <summary>画面に出すエラー</summary>
    public ErrorState Error { get; } = new();

    #endregion
}
