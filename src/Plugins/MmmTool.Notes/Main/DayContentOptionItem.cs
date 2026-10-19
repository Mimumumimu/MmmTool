using CommunityToolkit.Mvvm.ComponentModel;
using MmmTool.DayContent.Core;

namespace MmmTool.Notes.Main;

/// <summary>
/// 「表示する内容」の枠の中の、真偽の選択肢 1 つで、チェックとして出す。
/// </summary>
public sealed partial class DayContentOptionItem : ObservableObject
{
    /// <summary>変わったときに呼ぶ処理</summary>
    private Action<DayContentOptionItem>? _changed;

    /// <summary>選択肢を作る</summary>
    /// <param name="option">提供元が宣言した選択肢</param>
    /// <param name="isOn">最初の値</param>
    /// <param name="changed">値が変わったときに呼ぶ処理</param>
    public DayContentOptionItem(DayContentOption option, bool isOn, Action<DayContentOptionItem> changed)
    {
        Key = option.Key;
        Label = option.Label;
        IsOn = isOn;

        // 最初の値を入れた間は、変わったことにしない
        _changed = changed;
    }

    /// <summary>選択肢の識別 (この PC の設定のキー)</summary>
    public string Key { get; }

    /// <summary>画面に出す名前</summary>
    public string Label { get; }

    /// <summary>選んでいるか</summary>
    [ObservableProperty]
    public partial bool IsOn { get; set; }

    /// <summary>選びが変わったら、知らせる</summary>
    /// <param name="value">新しい値</param>
    partial void OnIsOnChanged(bool value) => _changed?.Invoke(this);
}
