namespace MmmTool.Users.Core;

/// <summary>管理者がユーザーごとに決める機能の利用の状態</summary>
/// <remarks>
/// 値は DB (<c>AppUserAdminSetting.Value</c>)にこの番号の文字列で保存する
/// 行が無い人は <see cref="Enabled"/>
/// </remarks>
public enum FeatureAccess
{
    /// <summary>無効</summary>
    /// <remarks>機能はそのユーザーにとって無いものになる</remarks>
    Disabled = 0,

    /// <summary>有効</summary>
    Enabled = 1,

    /// <summary>読み取り専用</summary>
    /// <remarks>強制は作っていないので、Enabled と同じ動きになる。</remarks>
    ReadOnly = 2,
}
