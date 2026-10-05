namespace MmmTool.Shell;

/// <summary>機能をオフにする前の確認（作業中の内容が失われるときに、機能が登録する）</summary>
/// <remarks>
/// <see cref="FeatureService.SetEnabledAsync"/> が、オフにする前に呼ぶ。確認が要らない機能は登録しない。
/// 機能をオフにしたあとの後始末は、<see cref="FeatureService.Changed"/> を受けるか、ページを捨てるとき（<see cref="IReleasablePage"/> と、ページを作ったスコープの破棄）に行う。
/// </remarks>
public interface IFeatureDisableConfirmation
{
    /// <summary>対象の機能のキー</summary>
    string FeatureKey { get; }

    /// <summary>オフにしてよいかを確かめる</summary>
    /// <returns>オフにしてよければ true（確認が要らないとき、確認で了承されたとき）。取りやめるときは false</returns>
    Task<bool> ConfirmAsync();
}
