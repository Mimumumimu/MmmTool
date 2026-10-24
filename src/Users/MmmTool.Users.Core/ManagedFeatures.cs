using MmmTool.CodeMaster.Core;

namespace MmmTool.Users.Core;

/// <summary>
/// 管理者が利用を決められる機能の一覧 (割り当ての台帳)
/// </summary>
/// <remarks>
/// 管理ツールの「機能の利用」は、この一覧から作る。機能を足すときは、機能番号を <see cref="CodeFeatures"/> に足してから、ここに 1 行足す。
/// 並びは、PC 版のサイドバーの初期の並び。DB を使わない機能は、サーバーモードの管理の対象にしないので載せない。
/// </remarks>
public static class ManagedFeatures
{
    /// <summary>選べる状態。読み取り専用は、強制を作るまで選ばせない (選んでも有効と同じ動きになるため)</summary>
    private static readonly IReadOnlyList<FeatureAccess> AllStates = [FeatureAccess.Enabled, FeatureAccess.Disabled];

    /// <summary>管理できる機能 (並びは、サイドバーの初期の並び)</summary>
    public static IReadOnlyList<ManagedFeature> All { get; } =
    [
        new(CodeFeatures.Notes, "ノート", AllStates),
        new(CodeFeatures.WorkItems, "作業管理", AllStates),
        new(CodeFeatures.Reminders, "リマインダー", AllStates),
        new(CodeFeatures.Messages, "メッセージ", AllStates),
    ];
}
