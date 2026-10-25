using CommunityToolkit.Mvvm.ComponentModel;
using MmmTool.Users.Core;

namespace MmmToolAdmin.Users;

/// <summary>
/// 機能の利用の一覧の 1 行。選んでいる人の、1 つの機能の状態を持つ。
/// </summary>
/// <remarks>状態を選び直すと、渡された通知 (<c>changed</c>)を呼ぶ。読み込んだ値を入れるとき (<see cref="Load"/>)は、呼ばない。</remarks>
public sealed partial class FeatureAccessItem : ObservableObject
{
    private readonly Action<FeatureAccessItem> _changed;
    private bool _loading;

    /// <summary>行を作る</summary>
    /// <param name="feature">機能</param>
    /// <param name="changed">状態を選び直したときの通知</param>
    /// <remarks>最初の状態は、選べる状態の先頭にする。保存はしない。</remarks>
    public FeatureAccessItem(ManagedFeature feature, Action<FeatureAccessItem> changed)
    {
        _changed = changed;
        FeatureNo = feature.FeatureNo;
        Name = feature.DisplayName;
        Options = [.. feature.Supported.Select(FeatureAccessOption.From)];
        Load(feature.Supported[0]);
    }

    /// <summary>機能番号</summary>
    public int FeatureNo { get; }

    /// <summary>機能の名前</summary>
    public string Name { get; }

    /// <summary>選べる状態</summary>
    public IReadOnlyList<FeatureAccessOption> Options { get; }

    /// <summary>選んでいる状態</summary>
    [ObservableProperty]
    public partial FeatureAccessOption Selected { get; set; }

    /// <summary>読み込んだ状態を入れる</summary>
    /// <param name="access">読み込んだ状態。選べない状態のときは、先頭の選択肢</param>
    public void Load(FeatureAccess access)
    {
        _loading = true;
        try
        {
            Selected = Options.FirstOrDefault(o => o.Access == access) ?? Options[0];
        }
        finally
        {
            _loading = false;
        }
    }

    /// <summary>選び直したら、保存を依頼する</summary>
    /// <param name="value">選んだ状態。選択肢の入れ替えで、空になることもある</param>
    partial void OnSelectedChanged(FeatureAccessOption value)
    {
        if (!_loading && value is not null)
        {
            _changed(this);
        }
    }
}
