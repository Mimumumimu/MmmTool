using Microsoft.Extensions.DependencyInjection;
using MmmSdk.WinUI.Components.Dialogs;
using MmmTool.Notes.Core;
using MmmTool.Notes.Edit;

namespace MmmTool.Notes;

/// <summary>ノートの入力ウィンドウを開く</summary>
/// <param name="scopeFactory">画面ごとの DI のスコープを作る</param>
/// <param name="dialogs">ダイアログの親を決めるサービス</param>
/// <remarks>
/// 画面は閉じると再表示できないので、開くたびに DI から作る。UI スレッドから呼ぶ。
/// 画面 (と ViewModel)は、開くたびに作るスコープから解決し、閉じたらスコープごと破棄する
/// (ルートから解決した <see cref="IDisposable"/> の Transient は、アプリの終了まで DI コンテナが保持し続けるため)。
/// </remarks>
public sealed class NoteDialogService(IServiceScopeFactory scopeFactory, IDialogHost dialogs) : INoteDialogService
{
    /// <inheritdoc />
    public async Task<bool> ShowEditAsync(Note? note, DateOnly defaultStartDate)
    {
        using var scope = scopeFactory.CreateScope();
        var window = scope.ServiceProvider.GetRequiredService<NoteEditWindow>();
        return await dialogs.ShowModalAsync(window, owner => window.ShowModalAsync(owner, note, defaultStartDate));
    }
}
