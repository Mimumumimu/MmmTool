using MmmTool.CodeMaster.Core;

namespace MmmTool.Users.Core;

/// <summary>管理者が利用を決められる機能の一覧</summary>
/// <remarks>
/// 管理ツールの「機能の利用」はこの一覧から作る。
/// 機能を足すときは機能番号を <see cref="CodeFeatures"/> に足してから、ここに 1 行足す。
/// 並びは PC 版のサイドバーの初期の並び。
/// DB を使わない機能はサーバーモードの管理の対象にしないので載せない。
/// </remarks>
public static class ManagedFeatures
{
    /// <summary>選べる状態</summary>
    /// <remarks>
    /// 選んでも有効と同じ動きになるため
    /// 読み取り専用は強制を作るまで選ばせない
    /// </remarks>
    private static readonly IReadOnlyList<FeatureAccess> AllStates = [FeatureAccess.Enabled, FeatureAccess.Disabled];

    /// <summary>管理できる機能</summary>
    public static IReadOnlyList<ManagedFeature> All { get; } =
    [
        new(CodeFeatures.Notes, "ノート", AllStates),
        new(CodeFeatures.WorkItems, "作業管理", AllStates),
        new(CodeFeatures.Reminders, "リマインダー", AllStates),
        new(CodeFeatures.Messages, "メッセージ", AllStates),
    ];
}
