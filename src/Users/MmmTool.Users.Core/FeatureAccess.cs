namespace MmmTool.Users.Core;

/// <summary>管理者が、ユーザーごとに決める、機能の利用の状態</summary>
/// <remarks>値は DB (<c>AppUserAdminSetting.Value</c>)に、この番号の文字列で保存する。行が無い人は <see cref="Enabled"/>。</remarks>
public enum FeatureAccess
{
    /// <summary>無効。機能は、そのユーザーにとって無いものになる</summary>
    Disabled = 0,

    /// <summary>有効</summary>
    Enabled = 1,

    /// <summary>読み取り専用。強制は作っていないので、<see cref="Enabled"/> と同じ動きになる</summary>
    ReadOnly = 2,
}
