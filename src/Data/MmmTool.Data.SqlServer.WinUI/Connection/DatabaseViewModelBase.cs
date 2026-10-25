using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MmmSdk.Core.Utilities;
using MmmSdk.WinUI.Components.Errors;

namespace MmmTool.Data.SqlServer.WinUI.Connection;

/// <summary>
/// DB を読む画面の ViewModel の共通部分。読み込みの失敗を出し、接続の設定が変わったら読み直す。
/// </summary>
/// <remarks>
/// 読み込み・書き込みの失敗 (接続できない・表が無い)は <see cref="Error"/> に出す。エラーの帯 (<see cref="ConnectionErrorBar"/>)に、この ViewModel を渡すと表示される。
/// 接続の設定の画面が閉じたら、どの画面から開いたかに関わらず、読み直す。ホストが最初の起動で自動で開いたときも、読み直すため。
/// 読み込みの世代を管理し、古い読み込みの結果で上書きしないために、<see cref="NextVersion"/> と <see cref="IsCurrent"/> を使う。UI スレッドで作ること。
/// </remarks>
public abstract partial class DatabaseViewModelBase : ObservableObject, IDisposable
{
    /// <summary>接続の設定の画面を開く</summary>
    private readonly IConnectionSettingsDialogService _dialogs;

    /// <summary>読み込みの世代。古い読み込みの結果で上書きしないためのもの</summary>
    private int _version;

    /// <summary>ViewModel を作り、接続の設定の画面が閉じたときの購読を始める</summary>
    /// <param name="dialogs">接続の設定の画面を開く</param>
    protected DatabaseViewModelBase(IConnectionSettingsDialogService dialogs)
    {
        _dialogs = dialogs;
        _dialogs.ConnectionSettingsClosed += OnConnectionSettingsClosed;
    }

    /// <summary>読み込み・書き込みの失敗のエラー</summary>
    public ErrorState Error { get; } = new();

    /// <summary>一覧などを読み込む</summary>
    /// <returns>読み込みの完了を表すタスク</returns>
    public abstract Task RefreshAsync();

    /// <summary>購読をやめる</summary>
    public virtual void Dispose()
    {
        _dialogs.ConnectionSettingsClosed -= OnConnectionSettingsClosed;
    }

    /// <summary>新しい読み込みを始める (これより前に始めた読み込みの結果は、捨てる)</summary>
    /// <returns>この読み込みの世代</returns>
    protected int NextVersion() => Interlocked.Increment(ref _version);

    /// <summary>この読み込みが、いちばん新しいか</summary>
    /// <param name="version">読み込みを始めたときの世代</param>
    /// <returns>いちばん新しければ true</returns>
    protected bool IsCurrent(int version) => version == Volatile.Read(ref _version);

    /// <summary>この画面から接続の設定の画面を開いて閉じたあとの処理。既定では、何もしない</summary>
    /// <returns>処理の完了を表すタスク</returns>
    /// <remarks>読み直しは、画面が閉じたときの購読が、すべての画面で行う。ここには、開いた画面だけが行う処理を書く。</remarks>
    protected virtual Task OnConnectionSettingsOpenedByThisAsync() => Task.CompletedTask;

    /// <summary>接続の設定の画面が閉じたら、読み直す</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="e">イベントの情報</param>
    private void OnConnectionSettingsClosed(object? sender, EventArgs e) => RefreshAsync().Forget();

    /// <summary>接続の設定の画面を開く</summary>
    /// <returns>画面が閉じるまでの待機を表すタスク</returns>
    [RelayCommand]
    private async Task OpenConnectionSettingsAsync()
    {
        await _dialogs.ShowConnectionSettingsAsync();
        await OnConnectionSettingsOpenedByThisAsync();
    }
}
