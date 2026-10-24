namespace MmmTool.CodeMaster.Core;

/// <summary>コードマスタの機能番号の一覧 (割り当ての台帳)</summary>
/// <remarks>
/// 機能の名前や表示の順が変わっても、番号は変えない (DB の意味が変わるため)。使い始める機能は、ここに足してから使う。
/// 区分番号・コード番号の割り当ては、その機能の Core が決める。
/// </remarks>
public static class CodeFeatures
{
    /// <summary>作業管理</summary>
    public const int WorkItems = 2;

    /// <summary>メッセージ。ユーザーごとの設定 (<c>AppUserSetting</c>)の機能番号にも使う</summary>
    public const int Messages = 3;
}
