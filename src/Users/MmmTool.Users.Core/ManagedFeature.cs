namespace MmmTool.Users.Core;

/// <summary>
/// 管理者が、ユーザーごとに、利用を決められる機能
/// </summary>
/// <param name="FeatureNo">機能番号で、<c>CodeMaster</c> と同じ台帳 (<c>CodeFeatures</c>)の番号</param>
/// <param name="DisplayName">管理ツールに出す機能の名前</param>
/// <param name="Supported">選べる状態。並びは、管理ツールの選択肢の並び</param>
public sealed record ManagedFeature(int FeatureNo, string DisplayName, IReadOnlyList<FeatureAccess> Supported);
