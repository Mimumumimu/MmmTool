using System.Globalization;
using MmmTool.Users.Core;

namespace MmmTool.Data.SqlServer.Users;

/// <summary>
/// 機能の利用 (<see cref="FeatureAccess"/>)を、ほかの問い合わせの条件に組み込むための SQL の断片。
/// </summary>
/// <remarks>
/// 宛先の一覧・送る処理・MmmBatch の送る直前の確認が、同じ条件で「機能が無効の人」を外すため、条件を 1 か所にまとめる。
/// 元になる表は <c>dbo.AppUserAdminSetting</c> で、区分は <see cref="FeatureAccessService.CategoryNo"/>、コード番号は <see cref="FeatureAccessService.CodeNo"/>。行が無い人は有効なので、無効に当たらない。
/// </remarks>
public static class FeatureAccessSql
{
    /// <summary>その人の機能が無効かを調べる条件を作る</summary>
    /// <param name="userExpression">人の番号を表す SQL の式。列名かパラメーター名で、利用者の入力を入れない</param>
    /// <param name="featureNo">機能番号</param>
    /// <returns>無効のとき真になる SQL の条件。別名は <c>fa</c> を使う</returns>
    public static string IsDisabled(string userExpression, int featureNo)
        => string.Create(CultureInfo.InvariantCulture,
            $"""
            EXISTS (SELECT 1 FROM dbo.AppUserAdminSetting AS fa
                    WHERE fa.UserId = {userExpression} AND fa.FeatureNo = {featureNo} AND fa.CategoryNo = {FeatureAccessService.CategoryNo} AND fa.CodeNo = {FeatureAccessService.CodeNo}
                        AND fa.Value = N'{(int)FeatureAccess.Disabled}' AND fa.IsDeleted = 0)
            """);
}
