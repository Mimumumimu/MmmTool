using Microsoft.Extensions.DependencyInjection;
using MmmTool.Data.Connection;

namespace MmmTool.Features.Database.Choice;

/// <summary>
/// 初回の保存先の選択の画面を開く。アプリ全体で 1 つ。
/// </summary>
/// <param name="scopeFactory">画面ごとの DI のスコープを作る</param>
/// <param name="settings">接続の設定の読み書き</param>
/// <remarks>
/// 保存先が、まだ保存されていないとき (初回)だけ、画面を出して、閉じるまで待つ。メインウィンドウを作ったあとに呼ぶ (作る前に出すと、閉じたときにアプリごと終了しうるため)。
/// 設定ファイルを読めなかったときは出さない (保存できず、毎回出てしまうため)。選んだ内容は、次の起動から反映する。
/// 画面は閉じると再表示できないので、開くたびに DI から作る。画面 (と ViewModel)は、開くたびに作るスコープから解決し、閉じたらスコープごと破棄する
/// (ルートから解決した <see cref="IDisposable"/> の Transient は、アプリの終了まで DI コンテナが保持し続けるため)。UI スレッドから呼ぶ。
/// </remarks>
public sealed class DatabaseChoiceService(IServiceScopeFactory scopeFactory, DatabaseSettingsService settings)
{
    /// <summary>保存先がまだ選ばれていなければ、選択の画面を出して、閉じるまで待つ</summary>
    /// <returns>画面を出した (閉じた)ことを表すタスク。出す必要が無ければ、すぐ完了する</returns>
    public async Task ShowIfNeededAsync()
    {
        if (settings.IsReadOnly || settings.IsModeSaved)
        {
            return;
        }

        using var scope = scopeFactory.CreateScope();
        var window = scope.ServiceProvider.GetRequiredService<DatabaseChoiceWindow>();
        await window.ShowAsync();
    }
}
