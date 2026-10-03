using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MmmTool.Services;

/// <summary>ダイアログの親を決めて、ダイアログ・モーダルウィンドウを開く</summary>
/// <remarks>
/// いちばん手前のモーダルウィンドウの上に表示する。無ければ、最後に操作した普通のウィンドウ（メインウィンドウ・リマインダーのメイン画面）の上に表示する。
/// モーダルウィンドウ（一覧・入力画面）は開いている間だけ覚えておき、その上で開くダイアログの親にする（一覧の上に入力画面・確認を重ねるため）。
/// 機能固有の画面は、各機能のサービスが <see cref="Owner"/> と <see cref="ShowModalAsync"/> を使って開く。UI スレッドから呼ぶ。
/// </remarks>
public sealed class DialogService : IDialogService
{
    /// <summary>開いているモーダルウィンドウ（開いた順）</summary>
    private readonly List<Window> _modals = [];

    /// <summary>親の候補にした普通のウィンドウ（登録順。閉じられたら外す）</summary>
    private readonly List<Window> _tracked = [];

    /// <summary>最後に操作した普通のウィンドウ。無い・閉じられたら null</summary>
    private Window? _lastActive;

    /// <summary>ダイアログの親</summary>
    /// <remarks>
    /// いちばん手前のモーダルウィンドウ、無ければ最後に操作した普通のウィンドウ、それも無ければ最初に登録したウィンドウ（起動時に登録するメインウィンドウ）。
    /// </remarks>
    /// <exception cref="InvalidOperationException">親にできるウィンドウが 1 つも登録されていない。</exception>
    public Window Owner => _modals.Count > 0
        ? _modals[^1]
        : _lastActive ?? _tracked.FirstOrDefault() ?? throw new InvalidOperationException("ダイアログの親にできるウィンドウがありません。");

    /// <summary>普通のウィンドウを、ダイアログの親の候補にする</summary>
    /// <param name="window">親の候補にするウィンドウ</param>
    /// <remarks>そのウィンドウを操作した（アクティブになった）ら、以後のダイアログをその上に出す。閉じられたら候補から外す。</remarks>
    public void TrackWindow(Window window)
    {
        _tracked.Add(window);
        window.Activated += (_, args) =>
        {
            if (args.WindowActivationState != WindowActivationState.Deactivated)
            {
                _lastActive = window;
            }
        };
        window.Closed += (_, _) =>
        {
            _tracked.Remove(window);
            if (_lastActive == window)
            {
                _lastActive = null;
            }
        };
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

    /// <summary>モーダルウィンドウを今の親の上に開き、閉じるまで覚えておく</summary>
    /// <typeparam name="T">ウィンドウが返す結果の型</typeparam>
    /// <param name="window">開くウィンドウ</param>
    /// <param name="show">親を受け取って表示し、閉じるまで待つ処理</param>
    /// <returns>ウィンドウが返した結果</returns>
    public async Task<T> ShowModalAsync<T>(Window window, Func<Window, Task<T>> show)
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
