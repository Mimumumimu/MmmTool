using Microsoft.Extensions.DependencyInjection;
using MmmSdk.WinUI.Components.Dialogs;
using MmmTool.CodeMaster.Core;

namespace MmmTool.CodeMaster.Edit;

/// <summary>コードマスタの名前の編集画面を開く</summary>
/// <param name="scopeFactory">画面ごとの DI のスコープを作る</param>
/// <param name="dialogs">ダイアログの親を決めるサービス</param>
/// <remarks>
/// 画面は閉じると再表示できないので、開くたびに DI から作る。UI スレッドから呼ぶ。
/// 画面と ViewModel は、開くたびに作るスコープから解決し、閉じたらスコープごと破棄する。
/// ルートから解決した <see cref="IDisposable"/> の Transient は、アプリの終了まで DI コンテナが保持し続けるため。
/// </remarks>
public sealed class CodeMasterDialogService(IServiceScopeFactory scopeFactory, IDialogHost dialogs) : ICodeMasterDialogService
{
    /// <inheritdoc />
    public async Task ShowEditAsync(CodeCategoryEditor editor)
    {
        using var scope = scopeFactory.CreateScope();
        var window = scope.ServiceProvider.GetRequiredService<CodeNameEditWindow>();
        await window.ViewModel.LoadAsync(editor);
        await dialogs.ShowModalAsync(window, async owner =>
        {
            await window.ShowModalAsync(owner);
            return true;
        });
    }
}
