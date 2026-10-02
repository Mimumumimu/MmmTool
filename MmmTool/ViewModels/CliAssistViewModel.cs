using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MmmTool.Core.Entities;
using MmmTool.Core.Repositories;
using MmmTool.Core.Services;
using MmmTool.Services;
using MmmTool.Services.Terminal;

namespace MmmTool.ViewModels;

public sealed partial class CliAssistViewModel : ObservableObject
{
    private readonly ICliCommandRepository _commandRepository;
    private readonly CliSettingsService _settings;
    private readonly AttachmentStore _attachmentStore;
    private readonly IImageConverter _imageConverter;
    private readonly IDialogService _dialogs;
    private readonly TimeProvider _timeProvider;

    private CliCommandSet _commandSet = new();
    private bool _initialized;

    public CliAssistViewModel(
        ITerminalSession terminal,
        ICliCommandRepository commandRepository,
        CliSettingsService settings,
        AttachmentStore attachmentStore,
        IImageConverter imageConverter,
        IDialogService dialogs,
        TimeProvider timeProvider)
    {
        Terminal = terminal;
        _commandRepository = commandRepository;
        _settings = settings;
        _attachmentStore = attachmentStore;
        _imageConverter = imageConverter;
        _dialogs = dialogs;
        _timeProvider = timeProvider;

        CommandItems = [];
        InputText = string.Empty;
        HistoryFilter = string.Empty;
        ErrorMessage = string.Empty;

        // 前回の作業ディレクトリでシェルを始める
        if (_settings.LastDirectory is { } lastDirectory && Directory.Exists(lastDirectory))
        {
            Terminal.WorkingDirectory = lastDirectory;
        }

        Attachments.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasAttachments));
        HistoryItems.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasNoHistory));
    }

    /// <summary>中央のターミナルで動くシェルのセッション。</summary>
    public ITerminalSession Terminal { get; }

    #region 定型コマンド

    /// <summary>左ペインで表示中のコマンド群（ターミナル / AI セッション）。</summary>
    [ObservableProperty]
    public partial CommandCategory SelectedCategory { get; set; }

    /// <summary>フォーカスの移動を View に頼む（ViewModel は UI に触れないため）。</summary>
    public event EventHandler<FocusTarget>? FocusRequested;

    /// <summary>左ペインのツリーに表示する要素。</summary>
    [ObservableProperty]
    public partial IReadOnlyList<CommandTreeItem> CommandItems { get; private set; }

    public async Task InitializeAsync()
    {
        if (_initialized)
        {
            return;
        }
        _initialized = true;

        if (_settings.LoadError is { } loadError)
        {
            ShowError(loadError);
        }

        try
        {
            _commandSet = await _commandRepository.LoadAsync();
        }
        catch (DataFileException ex)
        {
            ShowError($"{ex.Message}\n定型コマンドは表示されません。");
        }
        RebuildCommandItems();
    }

    partial void OnSelectedCategoryChanged(CommandCategory value) => RebuildCommandItems();

    private void RebuildCommandItems()
    {
        var items = new List<CommandTreeItem>();
        var nodes = SelectedCategory == CommandCategory.Terminal ? _commandSet.Terminal : _commandSet.Session;

        // 「作業ディレクトリ変更」はターミナル側の先頭に固定で置く（定義ファイルには含めない）
        if (SelectedCategory == CommandCategory.Terminal)
        {
            items.Add(CommandTreeItem.ChangeDirectory());
        }
        items.AddRange((nodes ?? []).Select(CommandTreeItem.From));
        CommandItems = items;
    }

    /// <summary>ツリーの要素を実行する（コマンドなら送信、作業ディレクトリ変更ならダイアログ）。</summary>
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

    private async Task ChangeWorkingDirectoryAsync()
    {
        if (await _dialogs.ShowWorkingDirectoryDialogAsync() is not { } directory)
        {
            return;
        }

        Terminal.Submit(ShellCommands.ChangeDirectory(ShellCommands.DetectKind(Terminal.CommandLine), directory));
        // シェルを再起動したときも同じ場所から始める
        Terminal.WorkingDirectory = directory;

        try
        {
            await _settings.AddDirectoryAsync(directory);
        }
        catch (DataFileException ex)
        {
            ShowError(ex.Message);
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

    public ObservableCollection<AttachmentItem> Attachments { get; } = [];

    public bool HasAttachments => Attachments.Count > 0;

    /// <summary>ファイルを添付する（ドラッグ＆ドロップ・ファイルの貼り付け）。</summary>
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
                ShowError($"{Path.GetFileName(filePath)} を添付できませんでした。{ex.Message}");
            }
        }
    }

    /// <summary>画像を JPEG にして添付する（画像の貼り付け）。</summary>
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
            ShowError($"画像を添付できませんでした。{ex.Message}");
        }
    }

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
            ShowError($"添付ファイルを削除できませんでした。{ex.Message}");
        }
    }

    #endregion

    #region 送信履歴

    private const int MaxSendHistory = 50;

    /// <summary>送信履歴（先頭が最新）。プライバシーのため保存せず、起動中だけ持つ。</summary>
    private readonly List<(string Text, DateTimeOffset SentAt)> _sendHistory = [];

    public ObservableCollection<SendHistoryItem> HistoryItems { get; } = [];

    public bool HasNoHistory => HistoryItems.Count == 0;

    /// <summary>履歴の絞り込み文字列。</summary>
    [ObservableProperty]
    public partial string HistoryFilter { get; set; }

    partial void OnHistoryFilterChanged(string value) => RefreshHistory();

    /// <summary>履歴の一覧を開く前に、最新の内容にする。</summary>
    public void PrepareHistory()
    {
        HistoryFilter = string.Empty;
        RefreshHistory();
    }

    /// <summary>履歴の本文を入力欄へ戻す。</summary>
    public void RestoreHistory(SendHistoryItem item) => InputText = item.Text;

    /// <summary>送信した本文を履歴の先頭に追加する。空白のみは無視し、同じ本文は先頭へ移す。</summary>
    private void AddSendHistory(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        _sendHistory.RemoveAll(entry => entry.Text == text);
        _sendHistory.Insert(0, (text, _timeProvider.GetLocalNow()));
        if (_sendHistory.Count > MaxSendHistory)
        {
            _sendHistory.RemoveRange(MaxSendHistory, _sendHistory.Count - MaxSendHistory);
        }
    }

    private void RefreshHistory()
    {
        var today = _timeProvider.GetLocalNow().Date;
        var filter = HistoryFilter.Trim();

        HistoryItems.Clear();
        foreach (var entry in _sendHistory)
        {
            if (filter.Length > 0 && !entry.Text.Contains(filter, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var sentAt = entry.SentAt.ToLocalTime();
            var sentAtText = sentAt.Date == today ? sentAt.ToString("HH:mm") : sentAt.ToString("M/d HH:mm");
            HistoryItems.Add(new SendHistoryItem(entry.Text, SendText.ToSingleLine(entry.Text), sentAtText));
        }
    }

    #endregion

    #region エラー表示

    [ObservableProperty]
    public partial string ErrorMessage { get; set; }

    [ObservableProperty]
    public partial bool IsErrorOpen { get; set; }

    private void ShowError(string message)
    {
        ErrorMessage = message;
        IsErrorOpen = true;
    }

    #endregion
}
