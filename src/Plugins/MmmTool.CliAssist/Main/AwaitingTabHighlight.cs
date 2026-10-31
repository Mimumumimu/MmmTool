using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.UI;

namespace MmmTool.CliAssist.Main;

/// <summary>選ばれていないタブで確認が出たとき、タブの背景を明滅させて黄色にする</summary>
/// <remarks>
/// 明滅は 3 回で、止まったあとは黄色のまま残す。残すのは、出た瞬間を見逃しても、タブを選ぶまで分かるようにするため。
/// 黄色が残っている間にテーマを切り替えたときは、新しいテーマの黄色に付け替える。このときは明滅しない。
/// </remarks>
internal sealed class AwaitingTabHighlight
{
    /// <summary>明滅の回数</summary>
    private const int FlashCount = 3;
    /// <summary>明滅の片道の時間</summary>
    /// <remarks>往復で 0.4 秒になり、1 秒に 3 回未満に収まる。</remarks>
    private static readonly Duration FlashHalfDuration = new(TimeSpan.FromMilliseconds(200));

    /// <summary>対象のタブ</summary>
    private readonly TabViewItem _item;
    /// <summary>色を持つリソース</summary>
    private readonly ResourceDictionary _resources;

    /// <summary>動いている明滅</summary>
    private Storyboard? _flash;
    /// <summary>確認を待っているか</summary>
    private bool _awaiting;

    /// <summary>タブの強調を作る</summary>
    /// <param name="item">対象のタブ</param>
    /// <param name="resources">色を持つリソース。<c>SessionAwaitingTabColor</c> と <c>SessionAwaitingTabFlashColor</c> を、ライトとダークで持つ。</param>
    public AwaitingTabHighlight(TabViewItem item, ResourceDictionary resources)
    {
        _item = item;
        _resources = resources;
        _item.ActualThemeChanged += OnThemeChanged;
    }

    /// <summary>黄色の印を付ける・外す</summary>
    /// <param name="awaiting">確認を待っているなら true</param>
    public void Set(bool awaiting)
    {
        _awaiting = awaiting;
        Apply(flash: true);
    }

    /// <summary>タブを取り除くとき、テーマの切り替えの購読を外し、明滅を止める</summary>
    public void Detach()
    {
        _item.ActualThemeChanged -= OnThemeChanged;
        _flash?.Stop();
        _flash = null;
    }

    /// <summary>テーマが切り替わったら、待っている間だけ、新しいテーマの黄色に付け替える</summary>
    /// <param name="sender">イベントの送信元</param>
    /// <param name="args">イベントの情報</param>
    private void OnThemeChanged(FrameworkElement sender, object args)
    {
        if (_awaiting)
        {
            Apply(flash: false);
        }
    }

    /// <summary>今の状態に合わせて、タブの背景を決める</summary>
    /// <param name="flash">明滅させるか</param>
    private void Apply(bool flash)
    {
        _flash?.Stop();
        _flash = null;
        if (!_awaiting)
        {
            _item.ClearValue(Control.BackgroundProperty);
            return;
        }

        var colors = (ResourceDictionary)_resources.ThemeDictionaries[_item.ActualTheme == ElementTheme.Dark ? "Dark" : "Light"];
        var steady = (Color)colors["SessionAwaitingTabColor"];

        // 明滅は、セッションごとの専用のブラシで行う (共有のブラシを動かすと、ほかのタブまで光るため)
        var brush = new SolidColorBrush(steady);
        _item.Background = brush;
        if (!flash)
        {
            return;
        }

        var animation = new ColorAnimation
        {
            From = steady,
            To = (Color)colors["SessionAwaitingTabFlashColor"],
            Duration = FlashHalfDuration,
            AutoReverse = true,
            RepeatBehavior = new RepeatBehavior(FlashCount),
        };
        Storyboard.SetTarget(animation, brush);
        Storyboard.SetTargetProperty(animation, "Color");
        var storyboard = new Storyboard();
        storyboard.Children.Add(animation);
        storyboard.Begin();
        _flash = storyboard;
    }
}
