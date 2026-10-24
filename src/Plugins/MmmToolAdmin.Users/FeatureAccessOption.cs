using MmmTool.Users.Core;

namespace MmmToolAdmin.Users;

/// <summary>機能の利用の選択肢 (コンボボックスの 1 項目)</summary>
/// <param name="Access">状態</param>
/// <param name="Label">画面に出す名前</param>
public sealed record FeatureAccessOption(FeatureAccess Access, string Label)
{
    /// <summary>状態から、選択肢を作る</summary>
    /// <param name="access">状態</param>
    /// <returns>選択肢</returns>
    public static FeatureAccessOption From(FeatureAccess access) => new(access, access switch
    {
        FeatureAccess.Enabled => "有効",
        FeatureAccess.ReadOnly => "読み取り専用",
        _ => "無効",
    });
}
