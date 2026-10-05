using Microsoft.UI.Xaml.Controls;
using MmmTool.Core.CliAssist;

namespace MmmTool.Features.CliAssist.Setup;

/// <summary>CLI補助の初期設定ダイアログ (使うツールと、動かす環境を選ぶ)</summary>
public sealed partial class CliSetupDialog : ContentDialog
{
    /// <summary>初回として開いたか</summary>
    /// <remarks>初回は、選ばずに閉じられないようにする (定型コマンドのファイルを作るのに、選んだ内容が要るため)。</remarks>
    private bool _isFirstRun;

    /// <summary>ダイアログの ViewModel</summary>
    public CliSetupDialogViewModel ViewModel { get; }

    /// <summary>ダイアログを作る</summary>
    /// <param name="viewModel">ダイアログの ViewModel</param>
    public CliSetupDialog(CliSetupDialogViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    /// <summary>初回の初期設定として開く</summary>
    /// <returns>選んだ初期設定</returns>
    /// <remarks>キャンセルのボタンは出さず、Esc でも閉じない (「作成」で閉じるまで待つ)。</remarks>
    public async Task<CliSetup> ShowFirstRunAsync()
    {
        _isFirstRun = true;
        ViewModel.Initialize(CliEnvironment.Windows, isReset: false);
        Title = "CLI補助の初期設定";
        PrimaryButtonText = "作成";
        DefaultButton = ContentDialogButton.Primary;

        await ShowAsync();
        return ViewModel.ToSetup();
    }

    /// <summary>初期化 (定型コマンドの作り直し)として開く</summary>
    /// <param name="currentEnvironment">今の環境 (最初に選んでおく)</param>
    /// <returns>選んだ初期設定。キャンセルなら null</returns>
    /// <remarks>取り消せない操作なので、既定のボタンを置かない (Enter で誤って実行しない。キャンセルを既定にすると強調色になり、主な操作に見えるため。SDK の確認ダイアログと同じ)。</remarks>
    public async Task<CliSetup?> ShowResetAsync(CliEnvironment currentEnvironment)
    {
        _isFirstRun = false;
        ViewModel.Initialize(currentEnvironment, isReset: true);
        Title = "定型コマンドを初期化";
        PrimaryButtonText = "初期化";
        CloseButtonText = "キャンセル";
        DefaultButton = ContentDialogButton.None;

        return await ShowAsync() == ContentDialogResult.Primary ? ViewModel.ToSetup() : null;
    }

    /// <summary>閉じる前の処理 (初回は、決定のボタン以外では閉じない)</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="args">閉じる操作の情報</param>
    private void OnClosing(ContentDialog sender, ContentDialogClosingEventArgs args)
    {
        if (_isFirstRun && args.Result != ContentDialogResult.Primary)
        {
            args.Cancel = true;
        }
    }
}
