using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MmmTool.Core.Entities;
using MmmTool.Views;
using MmmTool.Views.Dialogs;

namespace MmmTool.Services;

/// <summary>ダイアログを開く</summary>
/// <remarks>
/// いちばん手前のモーダルウィンドウの上に表示する。無ければ、最後に操作した普通のウィンドウ（メインウィンドウ・リマインダーのメイン画面）の上に表示する。
/// モーダルウィンドウ（一覧・入力画面）は開いている間だけ覚えておき、その上で開くダイアログの親にする（一覧の上に入力画面・確認を重ねるため）。
/// UI スレッドから呼ぶ。
/// </remarks>
public sealed class DialogService(IServiceProvider services) : IDialogService
{
    /// <summary>開いているモーダルウィンドウ（開いた順）</summary>
    private readonly List<Window> _modals = [];

    /// <summary>最後に操作した普通のウィンドウ。無い・閉じられたら null</summary>
    private Window? _lastActive;

    /// <summary>ダイアログの親（いちばん手前のモーダルウィンドウ、無ければ最後に操作した普通のウィンドウ、それも無ければメインウィンドウ）</summary>
    private Window Owner => _modals.Count > 0 ? _modals[^1] : _lastActive ?? services.GetRequiredService<MainWindow>();

    /// <summary>普通のウィンドウを、ダイアログの親の候補にする</summary>
    /// <remarks>そのウィンドウを操作した（アクティブになった）ら、以後のダイアログをその上に出す。閉じられたら候補から外す。</remarks>
    public void TrackWindow(Window window)
    {
        window.Activated += (_, args) =>
        {
            if (args.WindowActivationState != WindowActivationState.Deactivated)
            {
                _lastActive = window;
            }
        };
        window.Closed += (_, _) =>
        {
            if (_lastActive == window)
            {
                _lastActive = null;
            }
        };
    }

    /// <inheritdoc />
    public Task<string?> ShowWorkingDirectoryDialogAsync()
    {
        var dialog = services.GetRequiredService<WorkingDirectoryDialog>();
        dialog.XamlRoot = Owner.Content.XamlRoot;
        return dialog.PickAsync();
    }

    /// <inheritdoc />
    public async Task<bool> ConfirmAsync(string title, string message, string primaryText)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = Owner.Content.XamlRoot,
            // コードで作るときは既定のスタイルが当たらないため、明示する（付けないと旧来の見た目になる）
            Style = (Style)Application.Current.Resources["DefaultContentDialogStyle"],
            Title = title,
            Content = message,
            PrimaryButtonText = primaryText,
            CloseButtonText = "キャンセル",
            // 取り消しにくい操作なので、Enter で誤って実行しないようキャンセルを既定にする
            DefaultButton = ContentDialogButton.Close,
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    /// <inheritdoc />
    public Task<Reminder?> ShowReminderInputAsync(Reminder? reminder)
    {
        var window = services.GetRequiredService<ReminderInputWindow>();
        return ShowModalAsync(window, owner => window.ShowModalAsync(owner, reminder));
    }

    /// <inheritdoc />
    public Task ShowReminderListAsync()
    {
        var window = services.GetRequiredService<ReminderListWindow>();
        return ShowModalAsync(window, async owner =>
        {
            await window.ShowModalAsync(owner);
            return true;
        });
    }

    /// <summary>モーダルウィンドウを今の親の上に開き、閉じるまで覚えておく</summary>
    /// <param name="window">開くウィンドウ</param>
    /// <param name="show">親を受け取って表示し、閉じるまで待つ処理</param>
    private async Task<T> ShowModalAsync<T>(Window window, Func<Window, Task<T>> show)
    {
        var owner = Owner;
        _modals.Add(window);
        try
        {
            return await show(owner);
        }
        finally
        {
            _modals.Remove(window);
        }
    }
}
