using Microsoft.Extensions.DependencyInjection;
using MmmSdk.Core.Components.Settings;
using MmmTool.Data.Connection;

namespace MmmTool.Features.Database.Choice;

/// <summary>
/// 初回の保存先の選択の画面を開く。アプリ全体で 1 つ。
/// </summary>
/// <param name="scopeFactory">画面ごとの DI のスコープを作る</param>
/// <param name="settings">接続の設定の読み書き</param>
/// <param name="settingsStore">汎用設定ストア (読み込み済みにしてから、保存先を調べる)</param>
/// <remarks>
/// 保存先が、まだ保存されていないとき (初回)だけ、画面を出して、選ぶまで待つ。各機能の起動時の準備より前に呼ぶ (保存先は準備で決まって使い始めるので、選んだ内容を最初から効かせるため)。
/// 設定ファイルを読めなかったときは出さない (保存できず、毎回出てしまうため)。
/// 「決定」では、画面を閉じずに隠したまま残す (最初のウィンドウなので、閉じるとアプリごと終了しうるため)。メインウィンドウを作ったあとに <see cref="CloseWindow"/> で閉じる。
/// 画面は閉じると再表示できないので、DI から作る。画面 (と ViewModel)は、作ったスコープから解決し、閉じたらスコープごと破棄する
/// (ルートから解決した <see cref="IDisposable"/> の Transient は、アプリの終了まで DI コンテナが保持し続けるため)。UI スレッドから呼ぶ。
/// </remarks>
public sealed class DatabaseChoiceService(IServiceScopeFactory scopeFactory, DatabaseSettingsService settings, ISettingsStore settingsStore)
{
    /// <summary>画面を作ったスコープ。画面を出していなければ null</summary>
    private IServiceScope? _scope;

    /// <summary>隠して残している画面。無ければ null</summary>
    private DatabaseChoiceWindow? _window;

    /// <summary>保存先がまだ選ばれていなければ、選択の画面を出して、選ぶまで待つ</summary>
    /// <returns>先へ進んでよければ true (選んで保存した・選ぶ必要が無かった)。選ばずに閉じたら false (アプリを終了する)</returns>
    public async Task<bool> ShowIfNeededAsync()
    {
        // 設定ファイルを、UI スレッドを止めずに読み込んでおく (読めなかったときは IsReadOnly になる)
        await settingsStore.EnsureLoadedAsync();
        if (settings.IsReadOnly || settings.IsModeSaved)
        {
            return true;
        }

        _scope = scopeFactory.CreateScope();
        _window = _scope.ServiceProvider.GetRequiredService<DatabaseChoiceWindow>();
        if (await _window.ShowAsync())
        {
            return true;
        }

        // × で閉じた (画面はもう閉じている)
        Release();
        return false;
    }

    /// <summary>隠して残している画面を閉じる</summary>
    /// <remarks>メインウィンドウを作ったあとに呼ぶ。画面が無ければ何もしない。</remarks>
    public void CloseWindow()
    {
        _window?.Close();
        Release();
    }

    /// <summary>画面とスコープの参照を手放す</summary>
    private void Release()
    {
        _window = null;
        _scope?.Dispose();
        _scope = null;
    }
}
