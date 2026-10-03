using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Xaml;
using MmmSdk.Core;
using MmmSdk.Core.SingleInstance;
using MmmSdk.WinUI.Tray;
using MmmSdk.WinUI;
using MmmSdk.WinUI.Dialogs;
using MmmTool.Features.CliAssist;
using MmmTool.Features.Debugging;
using MmmTool.Features.Links;
using MmmTool.Features.Reminders;
using MmmTool.Features.Settings;
using MmmTool.Shell;

namespace MmmTool;

/// <summary>アプリケーション（起動・終了と DI の構成）</summary>
public partial class App : Application
{
    /// <summary>多重起動の防止</summary>
    private readonly SingleInstanceGuard _instanceGuard = new("MmmTool");
    /// <summary>DI・ログなどを扱う Host</summary>
    private readonly IHost _host;
    /// <summary>メインウィンドウ。まだ作っていなければ null</summary>
    private MainWindow? _window;

    /// <summary>多重起動を確認して、DI の構成を作る</summary>
    public App()
    {
        if (!_instanceGuard.IsFirstInstance)
        {
            NativeMessageBox.ShowInformation("MmmTool はすでに起動しています。", "MmmTool");
            Environment.Exit(0);
        }

        InitializeComponent();

        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            ContentRootPath = AppContext.BaseDirectory,
        });
        ConfigureServices(builder.Services);
        _host = builder.Build();
    }

    /// <summary>DI に登録する</summary>
    /// <param name="services">登録先のサービスコレクション</param>
    /// <remarks>機能ごとの中身（保存先・サービス・画面）は、各機能の Add～ メソッドにある。</remarks>
    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);

        // SDK（JsonFileStore・設定ストア・位置保存・パスを開く処理・通知ダイアログ・確認ダイアログ・ファイル/フォルダー選択）。JsonFileStore はアプリ固有の保存でも共有する
        services.AddMmmSdkCore(Path.Combine(AppContext.BaseDirectory, "Data"));
        services.AddMmmSdkWinUI();

        // 画面の枠（メインウィンドウ・サイドバー・トレイ）
        services.AddShell();

        // 機能。登録した順に、サイドバーの項目（上部・下部それぞれ）・トレイメニューの項目・起動時の準備が並ぶ
        services.AddCliAssist();
        services.AddReminders();
        services.AddLinks();
        services.AddDebugging();
        services.AddSettings();
    }

    /// <inheritdoc />
    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        await _host.StartAsync();

        // トレイは画面・各機能より先に作る。Host は作った順の逆に破棄するので、終了時に各機能の後始末が済んでからトレイアイコンが消える
        var tray = _host.Services.GetRequiredService<TrayIcon>();

        // 各機能の起動時の準備（設定の読み込み・時刻監視の開始など）。画面を作る前に行う（前回の作業ディレクトリでシェルを始めるため等）
        foreach (var startup in _host.Services.GetServices<IStartupTask>())
        {
            await startup.StartAsync();
        }

        _window = _host.Services.GetRequiredService<MainWindow>();
        // メインウィンドウから開くダイアログを、メインウィンドウの上に出すため（リマインダーのメイン画面と使い分ける）
        _host.Services.GetRequiredService<IDialogHost>().TrackWindow(_window);

        tray.OpenRequested += (_, _) => _window.ShowAndActivate();
        tray.ExitRequested += async (_, _) => await ExitAsync();
        // 起動時はトレイだけ。ウィンドウはトレイから開いたときに初めて出す
        tray.Show();
    }

    /// <summary>アプリを完全に終了する（トレイメニューの「終了」から）。</summary>
    /// <returns>終了処理の完了を表すタスク</returns>
    /// <remarks>
    /// 各機能の後始末（ターミナルのシェル・添付の一時フォルダ等）→ トレイアイコンの解放 → アプリの終了 の順に行う。
    /// 後の 2 つは Host の破棄（作った順の逆）で行われる。未保存の編集内容は確認せずに破棄する。
    /// </remarks>
    private async Task ExitAsync()
    {
        _window?.PrepareExit();
        await _host.StopAsync();
        _host.Dispose();
        Exit();
    }
}
