using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using MmmSdk.Core;
using MmmSdk.Core.Components.Features;
using MmmSdk.WinUI;
using MmmSdk.WinUI.Components.Dialogs;
using MmmSdk.WinUI.Components.Hosting;
using MmmSdk.WinUI.Components.Pages;
using MmmSdk.WinUI.Components.Tray;
using MmmSdk.WinUI.Utilities;
using MmmTool.Backlog;
using MmmTool.CliAssist;
using MmmTool.ClipboardTransfer;
using MmmTool.CodeMaster;
using MmmTool.Data.SqlServer;
using MmmTool.Data.Storage;
using MmmTool.Features.Database;
using MmmTool.Features.Database.Choice;
using MmmTool.Features.Settings;
using MmmTool.Features.Users;
using MmmTool.Links;
using MmmTool.NotificationChannels;
using MmmTool.Reminders;
using MmmTool.Shell;
using MmmTool.Shell.Main;
using MmmTool.Tools;
using MmmTool.WorkItems;

namespace MmmTool;

/// <summary>アプリケーション</summary>
public partial class App : Application
{
    /// <summary>起動と終了の骨格</summary>
    private readonly AppHost _appHost;
    /// <summary>メインウィンドウ</summary>
    /// <remarks>まだ作っていなければ null</remarks>
    private MainWindow? _window;

    /// <summary>多重起動を確認して DI の構成を作る</summary>
    public App()
    {
        _appHost = new AppHost(this, AppInfo.Name, AppInfo.LogDirectory);
        InitializeComponent();
        _appHost.Build(ConfigureServices);
    }

    /// <summary>DI に登録する</summary>
    /// <param name="services">登録先のサービスコレクション</param>
    /// <remarks>機能ごとの中身 (保存先・サービス・画面)は各機能の Add～ メソッドにある</remarks>
    private static void ConfigureServices(IServiceCollection services)
    {
        // SDK の部品は、JsonFileStore・設定ストア・位置保存・パスを開く処理・通知ダイアログ・確認ダイアログ・ファイル/フォルダー選択。JsonFileStore はアプリ固有の保存でも共有する
        services.AddMmmSdkCore(AppInfo.DataDirectory);
        services.AddMmmSdkWinUI();

        // 画面の枠 (メインウィンドウ・サイドバー・トレイ)
        services.AddShell();

        // 設定ページの先頭の部品は、全般・機能の一覧。設定ページの部品は並び順の値で並ぶので、呼ぶ位置は問わない
        services.AddSettingsSections();

        // DB モードのログイン。機能の起動時の準備より先に動かすため、機能の登録より前に呼ぶ
        services.AddUserSignIn();

        // 機能。起動時の準備は、登録した順に実行する。order は、保存が無いときの並び。利用者が決めた並びがあれば、サイドバー・設定の機能の一覧・設定の部品はそちらに従う。トレイメニューは常に order の順。登録の順は問わない
        // order の並び: 作業管理が主な機能。ノート、CLI補助、リマインダーと続く。メッセージはリマインダーの下。トレイは、メニューが下から開くので、下 (大きい値)ほどカーソルに近い。リンクを下にして近くに置く
        services.AddFeaturePlugin<WorkItemsPlugin>(order: 1);
        services.AddFeaturePlugin<MmmTool.Notes.NotesPlugin>(order: 2);
        services.AddFeaturePlugin<CliAssistPlugin>(order: 3);
        services.AddFeaturePlugin<RemindersPlugin>(order: 4, trayOrder: 2);
        services.AddFeaturePlugin<MmmTool.Messages.MessagesPlugin>(order: 5);
        services.AddFeaturePlugin<LinksPlugin>(order: 6, trayOrder: 3);
        services.AddFeaturePlugin<ClipboardTransferPlugin>(order: 7);
        services.AddFeaturePlugin<BacklogPlugin>(order: 8);
        services.AddFeaturePlugin<ToolsPlugin>(order: 9);

        // SQL Server への保存。機能が登録した JSON の保存先を、設定が DB のときだけ置き換えるので、機能の登録のあとに呼ぶ
        services.AddSqlServerData();

        // 作業管理・ノート・コードマスタの保存先。ローカルは SQLite の Data/MmmTool.db、DB モードは SQL Server。設定で選ぶ。保存先が増えたときに足すのは、この中の 1 行
        services.AddLocalAndServerStorage();

        // コードマスタの編集の画面。DB モードは編集できず、全員で共通なので管理ツールで直す。設定ページの「選択肢」は、編集できるときだけ出る
        services.AddCodeMasterSettings(SettingsSectionOrder.CodeMaster);

        // 送信先の一覧と登録の画面、設定ページの通知の節。どの機能にも属さない共通の部品で、サーバーモードだけ出る
        services.AddNotificationChannelScreens(SettingsSectionOrder.Notifications);

        // 保存先の画面 (設定ページの部品と、初回の選択)
        services.AddDatabaseScreens();

        services.AddSettings();
    }

    /// <inheritdoc />
    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        await _appHost.LaunchAsync(StartAsync);
    }

    /// <summary>Host を始めてトレイと各機能を用意する</summary>
    /// <returns>起動の完了を表すタスク</returns>
    /// <remarks>
    /// 読み込みの失敗など、起動を止めたくない例外は各機能の起動時の準備 (<see cref="MmmSdk.Core.Components.Features.IStartupTask"/>)の中で受け止め、画面を開いたときに知らせる。
    /// ここまで届いた例外は復旧できない失敗として <see cref="AppHost.LaunchAsync"/> が報告する。
    /// </remarks>
    private async Task StartAsync()
    {
        await _appHost.StartHostAsync();
        var services = _appHost.Services;

        // トレイは画面・各機能より先に作る。Host は作った順の逆に破棄するので、終了時に各機能の後始末が済んでからトレイアイコンが消える
        var tray = services.GetRequiredService<TrayIcon>();

        // 保存先がまだ選ばれていない初回だけ、選択の画面を出す。各機能の準備より前に出す。保存先は準備で決まって使い始めるため、選んだ内容を最初から効かせる
        // 選ばずに閉じたときは、先へ進ませずに終了する
        var databaseChoice = services.GetRequiredService<DatabaseChoiceService>();
        if (!await databaseChoice.ShowIfNeededAsync())
        {
            await ExitAsync();
            return;
        }

        // 各機能の起動時の準備で、設定の読み込み・時刻監視の開始など。画面を作る前に行う。前回の作業ディレクトリでシェルを始めるため等
        // オフにした機能の準備は行わない (オンにしたときに行う)
        await services.GetRequiredService<FeatureService>().StartAsync();

        _window = services.GetRequiredService<MainWindow>();
        // メインウィンドウから開くダイアログを、メインウィンドウの上に出すため (リマインダーのメイン画面と使い分ける)
        services.GetRequiredService<IDialogHost>().TrackWindow(_window);

        tray.OpenRequested += (_, _) => _window.BringToFront();
        // 通知のクリックなどで、機能がサイドバーのページを開くとき、トレイへ隠れているメインウィンドウも前面に出す
        services.GetRequiredService<INavigationService>().ShowRequested += (_, _) => _window.BringToFront();
        tray.ExitRequested += async (_, _) => await ExitAsync();
        // 設定ページなど、画面からの終了の依頼も、トレイの「終了」と同じ処理にする
        services.GetRequiredService<AppExitService>().ExitRequested += async (_, _) => await ExitAsync();

        // 保存先の選択の画面は、最後のウィンドウを閉じてアプリごと終了しないよう、メインウィンドウを作るまで隠して残していた。ここで閉じる
        databaseChoice.CloseWindow();

        // DB モードで、ログインできていないとき、ログインの画面を出す。メインウィンドウを作ったあとに出し、作る前に出すと、閉じたときにアプリごと終了しうる
        // ログインを済ませないと先へ進ませず、トレイも出さない。ログインせずに閉じたときは、終了する
        if (!await services.GetRequiredService<SignInPrompt>().ShowIfRequestedAsync())
        {
            await ExitAsync();
            return;
        }

        // 既定では、起動時はトレイだけ。ウィンドウはトレイから開いたときに初めて出す
        tray.Show();

        // メインウィンドウを出すのは、次のとき (ユーザーの登録が済んでから出す)
        // - 初回の起動で、保存先を選んだとき。トレイだけでは、何も起動していないように見えるため
        // - 設定で「起動時にメイン画面を開く」がオンのとき (設定ストアは、機能の準備の前に先読み済み)
        if (databaseChoice.HasChosen || services.GetRequiredService<MainWindowSettingsService>().OpenOnStartup)
        {
            _window.BringToFront();
        }
    }

    /// <summary>アプリを完全に終了する</summary>
    /// <returns>終了処理の完了を表すタスク</returns>
    /// <remarks>
    /// トレイメニューの「終了」から呼ぶ
    /// 各機能の後始末 (ターミナルのシェル・添付の一時フォルダ等)→ トレイアイコンの解放 → アプリの終了 の順に行う
    /// 後の 2 つは Host の破棄 (作った順の逆)で行われる
    /// 未保存の編集内容は確認せずに破棄する
    /// 順序と二重の呼び出しの防止は <see cref="AppHost.ExitAsync"/> が持つ
    /// </remarks>
    private Task ExitAsync() => _appHost.ExitAsync(_window, () => _window?.PrepareExit());
}
