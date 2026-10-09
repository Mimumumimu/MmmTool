using CommunityToolkit.Mvvm.ComponentModel;

namespace MmmTool.CliAssist.QuickMessages;

/// <summary>編集ダイアログで編集中の、よく使う文 1 件</summary>
public sealed partial class QuickMessageEditItem : ObservableObject
{
    /// <summary>表示名 (省略すると、文の 1 行目)</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayName))]
    public partial string Label { get; set; } = string.Empty;

    /// <summary>入力欄に入れる文</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayName))]
    public partial string Text { get; set; } = string.Empty;

    /// <summary>一覧に出す名前 (表示名、無ければ文の 1 行目。どちらも無ければ「(未入力)」)</summary>
    public string DisplayName
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(Label))
            {
                return Label.Trim();
            }
            var firstLine = Text.Split('\n').FirstOrDefault(line => !string.IsNullOrWhiteSpace(line));
            return firstLine is null ? "(未入力)" : firstLine.Trim();
        }
    }
}
