using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MmmSdk.Core.Attachments;
using MmmSdk.Core.Storage;
using MmmSdk.WinUI.Errors;
using MmmTool.Core.CliAssist;
using MmmTool.Features.CliAssist.Attachments;
using MmmTool.Features.CliAssist.Terminal;
using MmmTool.Features.CliAssist.WorkingDirectory;

namespace MmmTool.Features.CliAssist;

/// <summary>CLI補助ページの ViewModel</summary>
public sealed partial class CliAssistViewModel : ObservableObject
{
    /// <summary>定型コマンドの保存先</summary>
    private readonly ICliCommandRepository _commandRepository;
    /// <summary>CLI補助の利用状態</summary>
    private readonly CliSettingsService _settings;
    /// <summary>添付ファイルの一時保存先</summary>
    private readonly AttachmentStore _attachmentStore;
    /// <summary>画像の変換</summary>
    private readonly IImageConverter _imageConverter;
    /// <summary>作業ディレクトリ変更ダイアログ</summary>
    private readonly IWorkingDirectoryDialogService _workingDirectoryDialog;
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
    /// <param name="attachmentStore">添付ファイルの一時保存先</param>
    /// <param name="imageConverter">画像の変換</param>
    /// <param name="workingDirectoryDialog">作業ディレクトリ変更ダイアログを開く</param>
    /// <param name="timeProvider">現在時刻の提供元</param>
    public CliAssistViewModel(
        ITerminalSession terminal,
        ICliCommandRepository commandRepository,
        CliSettingsService settings,
        AttachmentStore attachmentStore,
        IImageConverter imageConverter,
        IWorkingDirectoryDialogService workingDirectoryDialog,
        TimeProvider timeProvider)
    {
        Terminal = terminal;
        _commandRepository = commandRepository;
        _settings = settings;
        _attachmentStore = attachmentStore;
        _imageConverter = imageConverter;
        _workingDirectoryDialog = workingDirectoryDialog;
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
    public ITerminalSession Terminal { get; }

    #region 定型コマンド

    /// <summary>左ペインで表示中のコマンド群（シェル / AI セッション）。</summary>
    [ObservableProperty]
    public partial CommandCategory SelectedCategory { get; set; }

    /// <summary>フォーカスの移動を View に頼む</summary>
    /// <remarks>ViewModel は UI に触れないため。</remarks>
    public event EventHandler<FocusTarget>? FocusRequested;

    /// <summary>左ペインのツリーに表示する要素。</summary>
    [ObservableProperty]
    public partial IReadOnlyList<CommandTreeItem> CommandItems { get; private set; }

    /// <summary>定型コマンドを読み込んで表示を初期化する</summary>
    /// <returns>初期化の完了を表すタスク</returns>
    /// <remarks>最初の 1 回だけ行う。</remarks>
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
        items.AddRange((nodes ?? []).Select(CommandTreeItem.From));
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
                Terminal.Submit(CommandPlaceholders.Expand(command, AppContext.BaseDirectory));
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

    /// <summary>作業ディレクトリ変更ダイアログを開き、選ばれたフォルダへ移動する</summary>
    /// <returns>作業ディレクトリの変更の完了を表すタスク</returns>
    private async Task ChangeWorkingDirectoryAsync()
    {
        if (await _workingDirectoryDialog.ShowAsync() is not { } directory)
        {
            return;
        }

        if (!ShellCommands.TryChangeDirectory(ShellCommands.DetectKind(Terminal.CommandLine), directory, out var command))
        {
            Error.Show($"cmd では、% を含むフォルダーへ移動できません（環境変数として展開されるため）: {directory}");
            return;
        }

        Terminal.Submit(command);
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
    [RelayCommand]
    private void Send()
    {
        var text = InputText;
        var attachmentPaths = Attachments.Select(item => item.FilePath).ToList();
        if (string.IsNullOrWhiteSpace(text) && attachmentPaths.Count == 0)
        {
            return;
        }

        Terminal.Submit(SendText.Compose(text, attachmentPaths));

        InputText = string.Empty;
        Attachments.Clear();
        _attachmentStore.CloseSession();

        // 履歴には入力欄の本文だけを残す（自動で付け足した指示文・パスは残さない）
        AddSendHistory(text.TrimEnd('\r', '\n'));
    }

    #endregion

    #region 添付

    /// <summary>添付ファイルの一覧</summary>
    public ObservableCollection<AttachmentItem> Attachments { get; } = [];

    /// <summary>添付があるか</summary>
    public bool HasAttachments => Attachments.Count > 0;

    /// <summary>ファイルを添付する（ドラッグ＆ドロップ・ファイルの貼り付け）。</summary>
    /// <param name="filePaths">添付するファイルのパス</param>
    /// <returns>添付の完了を表すタスク</returns>
    public async Task AddAttachmentFilesAsync(IEnumerable<string> filePaths)
    {
        foreach (var filePath in filePaths)
        {
            try
            {
                var savedPath = await _attachmentStore.AddFileAsync(filePath);
                Attachments.Add(new AttachmentItem(savedPath, Path.GetFileName(filePath)));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Error.Show($"{Path.GetFileName(filePath)} を添付できませんでした。{ex.Message}");
            }
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
            var savedPath = await _attachmentStore.AddAsync(jpeg, "clipboard.jpg");
            Attachments.Add(new AttachmentItem(savedPath, "貼り付けた画像"));
        }
        catch (Exception ex)
        {
            Error.Show($"画像を添付できませんでした。{ex.Message}");
        }
    }

    /// <summary>添付を取り除く</summary>
    /// <param name="item">取り除く添付</param>
    [RelayCommand]
    private void RemoveAttachment(AttachmentItem item)
    {
        Attachments.Remove(item);
        try
        {
            _attachmentStore.Remove(item.FilePath);
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
