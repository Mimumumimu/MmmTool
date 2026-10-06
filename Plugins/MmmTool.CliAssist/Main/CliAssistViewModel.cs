using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MmmSdk.Core.Components.Shells;
using MmmSdk.Core.Components.Storage;
using MmmSdk.Core.Utilities;
using MmmSdk.WinUI.Components.Dialogs;
using MmmTool.CliAssist.Core;
using MmmTool.CliAssist.Setup;
using MmmTool.CliAssist.WorkingDirectory;

namespace MmmTool.CliAssist.Main;

/// <summary>CLI補助ページの ViewModel</summary>
/// <remarks>
/// 定型コマンドと環境を持ち、セッション (タブ)の一覧を管理する。
/// ターミナル・入力欄・添付はセッションごと (<see cref="CliSessionViewModel"/>)に持つ。送信履歴はセッションをまたいで共通。
/// </remarks>
public sealed partial class CliAssistViewModel : ObservableObject
{
    /// <summary>定型コマンドの保存先</summary>
    private readonly ICliCommandRepository _commandRepository;
    /// <summary>CLI補助の利用状態</summary>
    private readonly CliSettingsService _settings;
    /// <summary>セッションを作る</summary>
    private readonly CliSessionFactory _sessionFactory;
    /// <summary>作業ディレクトリ変更ダイアログ</summary>
    private readonly IWorkingDirectoryDialogService _workingDirectoryDialog;
    /// <summary>初期設定ダイアログ</summary>
    private readonly ICliSetupDialogService _setupDialog;
    /// <summary>確認ダイアログ</summary>
    private readonly IDialogService _dialogs;

    /// <summary>読み込んだ定型コマンド</summary>
    private CliCommandSet _commandSet = new();
    /// <summary>初期化済みか</summary>
    private bool _initialized;

    /// <summary>ViewModel を作る</summary>
    /// <param name="commandRepository">定型コマンドの保存先</param>
    /// <param name="settings">CLI補助の利用状態</param>
    /// <param name="sessionFactory">セッションを作る</param>
    /// <param name="workingDirectoryDialog">作業ディレクトリ変更ダイアログを開く</param>
    /// <param name="setupDialog">初期設定ダイアログを開く</param>
    /// <param name="dialogs">確認ダイアログ</param>
    public CliAssistViewModel(
        ICliCommandRepository commandRepository,
        CliSettingsService settings,
        CliSessionFactory sessionFactory,
        IWorkingDirectoryDialogService workingDirectoryDialog,
        ICliSetupDialogService setupDialog,
        IDialogService dialogs)
    {
        _commandRepository = commandRepository;
        _settings = settings;
        _sessionFactory = sessionFactory;
        _workingDirectoryDialog = workingDirectoryDialog;
        _setupDialog = setupDialog;
        _dialogs = dialogs;

        CommandItems = [];
        Sessions.CollectionChanged += (_, _) => RefreshSessionTitles();

        // 最初のセッションは、前回の作業ディレクトリで始める
        var first = _sessionFactory.Create(_settings.StartDirectory);
        AddSession(first);
        SelectedSession = first;
    }

    #region セッション (タブ)

    /// <summary>セッション (タブ)の一覧 (タブの並び順)</summary>
    public ObservableCollection<CliSessionViewModel> Sessions { get; } = [];

    /// <summary>選ばれているセッション (中央に表示しているタブ)</summary>
    /// <remarks>定型コマンドの送信・作業ディレクトリ変更は、このセッションへ向ける。</remarks>
    [ObservableProperty]
    public partial CliSessionViewModel SelectedSession { get; set; }

    /// <summary>セッションを一覧に足す</summary>
    /// <param name="session">足すセッション</param>
    private void AddSession(CliSessionViewModel session)
    {
        // 作業ディレクトリが変わったら、タブ名を付け直す (同じフォルダ名のタブがあるかが変わるため)
        session.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(CliSessionViewModel.WorkingDirectory))
            {
                RefreshSessionTitles();
            }
        };
        Sessions.Add(session);
    }

    /// <summary>全タブのタブ名を付け直す</summary>
    private void RefreshSessionTitles()
    {
        var titles = SessionDirectory.CreateTitles([.. Sessions.Select(session => session.WorkingDirectory)]);
        for (var i = 0; i < Sessions.Count; i++)
        {
            Sessions[i].Title = titles[i];
        }
    }

    /// <summary>ほかのタブが開いているフォルダの一覧</summary>
    /// <param name="session">除くセッション (自分のタブ)。新しいタブを作るときは null</param>
    /// <returns>ほかのタブの作業ディレクトリ</returns>
    private List<string> DirectoriesOpenInOthers(CliSessionViewModel? session)
        => [.. Sessions.Where(other => other != session).Select(other => other.WorkingDirectory)];

    /// <summary>新しいタブを開く</summary>
    /// <returns>タブを開く操作の完了を表すタスク</returns>
    /// <remarks>
    /// 作業ディレクトリ変更ダイアログでフォルダを選んでから、そのフォルダでタブを作る (選ばずに作ると、ほかのタブと同じフォルダになりうるため)。
    /// ほかのタブが開いているフォルダは、ダイアログで断る。
    /// </remarks>
    [RelayCommand]
    private async Task AddSessionAsync()
    {
        if (await _workingDirectoryDialog.ShowAsync("新しいタブ", "開く", DirectoriesOpenInOthers(null)) is not { } directory)
        {
            return;
        }

        var session = _sessionFactory.Create(directory);
        session.Terminal.Shell = ShellFor(_commandSet);
        AddSession(session);
        SelectedSession = session;

        await RecordDirectoryAsync(session, directory);
    }

    /// <summary>タブを閉じる</summary>
    /// <param name="session">閉じるセッション</param>
    /// <returns>タブを閉じる操作の完了を表すタスク</returns>
    /// <remarks>
    /// 閉じる前に、いつも確認する (中で動いている CLI の有無は確実には分からないため)。
    /// 最後の 1 つは閉じない (ターミナルが 1 つも無い画面は意味が無いため)。
    /// </remarks>
    [RelayCommand]
    private async Task CloseSessionAsync(CliSessionViewModel session)
    {
        if (Sessions.Count <= 1 || !Sessions.Contains(session))
        {
            return;
        }

        var confirmed = await _dialogs.ConfirmAsync(
            "タブを閉じますか？",
            $"「{session.Title}」のターミナルと、実行中の CLI が終了します。",
            "閉じる",
            "キャンセル");
        // 確認の間にタブが閉じられた・最後の 1 つになったときは、何もしない
        if (!confirmed || Sessions.Count <= 1 || !Sessions.Contains(session))
        {
            return;
        }

        var index = Sessions.IndexOf(session);
        var wasSelected = session == SelectedSession;
        Sessions.Remove(session);
        if (wasSelected)
        {
            SelectedSession = Sessions[Math.Min(index, Sessions.Count - 1)];
        }

        // 画面からターミナルを外したあと (Sessions の変更を受けた画面が外す)に、シェルを終了する
        // シェルの終了待ち (数秒かかることがある)で UI スレッドを止めないよう、UI スレッドの外で行う
        Task.Run(session.Dispose).Forget();
    }

    #endregion

    #region 定型コマンド

    /// <summary>左ペインで表示中のコマンド群 (シェル / AI セッション)。</summary>
    [ObservableProperty]
    public partial CommandCategory SelectedCategory { get; set; }

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
            foreach (var session in Sessions)
            {
                session.Terminal.Shell = ShellFor(_commandSet);
            }
        }
        catch (DataFileException ex)
        {
            messages.Add($"{ex.Message}\n定型コマンドは表示されません。");
        }

        if (messages.OfType<string>().ToList() is { Count: > 0 } errors)
        {
            SelectedSession.Error.Show(string.Join("\n\n", errors));
        }
        RebuildCommandItems();
    }

    /// <summary>定型コマンドを既定の内容に作り直す (初期化)</summary>
    /// <returns>初期化の完了を表すタスク</returns>
    /// <remarks>
    /// 初期設定ダイアログ (警告を出して、確認を兼ねる)で、使うツールと環境を選び直し、その既定の内容で上書きする (今の内容は引き継がない)。
    /// 全タブに効く。ターミナルは、環境が変わらなくても、いつも起動し直す (ユーザーの決定)。動いている CLI は終了する。
    /// 環境が変わったときは、添付を外す (パスの形・一時保存先が環境ごとに違うため)。一時保存した添付は消す。
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
            SelectedSession.Error.Show(ex.Message);
            return;
        }
        _commandSet = commandSet;

        var shell = ShellFor(_commandSet);
        foreach (var session in Sessions)
        {
            if (shell.Kind != session.Terminal.Shell.Kind)
            {
                session.RemoveAllAttachments();
            }
            session.Terminal.Shell = shell;
        }

        SelectedCategory = CommandCategory.Shell;
        RebuildCommandItems();
        foreach (var session in Sessions)
        {
            session.RequestTerminalRestart();
        }
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

        // 「作業ディレクトリ変更」はシェル側の先頭に固定で置く (定義ファイルには含めない)
        if (SelectedCategory == CommandCategory.Shell)
        {
            items.Add(CommandTreeItem.ChangeDirectory());
        }
        items.AddRange(nodes.Select(CommandTreeItem.From));
        CommandItems = items;
    }

    /// <summary>ツリーの要素を実行する (コマンドなら送信、作業ディレクトリ変更ならダイアログ)。</summary>
    /// <param name="item">実行するツリーの要素</param>
    /// <returns>実行の完了を表すタスク</returns>
    /// <remarks>送信と作業ディレクトリ変更は、選ばれているタブへ向ける。</remarks>
    [RelayCommand]
    private async Task InvokeCommandItemAsync(CommandTreeItem item)
    {
        var session = SelectedSession;
        switch (item.Kind)
        {
            case CommandItemKind.Command when item.Command is { } command:
                if (!session.TrySubmit(CommandPlaceholders.Expand(command, session.AppDirectoryForShell())))
                {
                    break;
                }
                if (item.SwitchTo is { } tab)
                {
                    SelectedCategory = tab;
                }
                if (item.Focus is { } focus)
                {
                    session.RequestFocus(focus);
                }
                break;
            case CommandItemKind.ChangeDirectory:
                await ChangeWorkingDirectoryAsync(session);
                break;
        }
    }

    /// <summary>作業ディレクトリ変更ダイアログを開き、選ばれたフォルダへ移動する</summary>
    /// <param name="session">作業ディレクトリを変えるセッション</param>
    /// <returns>作業ディレクトリの変更の完了を表すタスク</returns>
    /// <remarks>ほかのタブが開いているフォルダは、ダイアログで断る。</remarks>
    private async Task ChangeWorkingDirectoryAsync(CliSessionViewModel session)
    {
        if (await _workingDirectoryDialog.ShowAsync("作業ディレクトリ変更", "変更", DirectoriesOpenInOthers(session)) is not { } directory)
        {
            return;
        }

        if (!session.TryChangeDirectory(directory))
        {
            return;
        }

        await RecordDirectoryAsync(session, directory);
    }

    /// <summary>選んだフォルダを、最近使ったフォルダの履歴に記録する</summary>
    /// <param name="session">記録に失敗したときの、エラーを出すセッション</param>
    /// <param name="directory">選んだフォルダ</param>
    /// <returns>記録の完了を表すタスク</returns>
    private async Task RecordDirectoryAsync(CliSessionViewModel session, string directory)
    {
        try
        {
            await _settings.AddDirectoryAsync(directory);
        }
        catch (DataFileException ex)
        {
            session.Error.Show(ex.Message);
        }
    }

    #endregion
}
