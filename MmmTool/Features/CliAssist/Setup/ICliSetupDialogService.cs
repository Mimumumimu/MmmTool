using MmmTool.Core.CliAssist;

namespace MmmTool.Features.CliAssist.Setup;

/// <summary>CLI補助の初期設定ダイアログ（ViewModel・Repository から UI 型に触れずに使うための口）</summary>
/// <remarks>UI スレッドから呼ぶ。</remarks>
public interface ICliSetupDialogService
{
    /// <summary>初回の初期設定として開く（定型コマンドのファイルが無い・壊れていたとき）</summary>
    /// <returns>選んだ初期設定</returns>
    /// <remarks>選ぶまで閉じない。</remarks>
    Task<CliSetup> ShowFirstRunAsync();

    /// <summary>初期化（定型コマンドの作り直し）として開く</summary>
    /// <param name="currentEnvironment">今の環境（最初に選んでおく）</param>
    /// <returns>選んだ初期設定。キャンセルなら null</returns>
    /// <remarks>消えるもの・終了するものの警告を出す（確認を兼ねる）。</remarks>
    Task<CliSetup?> ShowResetAsync(CliEnvironment currentEnvironment);
}
