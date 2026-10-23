using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using MmmTool.CliAssist.Core;

namespace MmmTool.CliAssist.QuickMessages;

/// <summary>編集ダイアログで編集中の、よく使う文またはグループ 1 件</summary>
/// <remarks>
/// 種類は作成時 (読み込み・追加時)に決めて変えない。
/// グループは文だけを持てて、グループの中にグループは入れない。
/// </remarks>
public sealed partial class QuickMessageEditItem : ObservableObject
{
    /// <summary>項目を作る</summary>
    /// <param name="label">表示名</param>
    /// <param name="text">入力欄に入れる文</param>
    /// <param name="children">グループの中の文 (文は null)</param>
    /// <remarks>生成は <see cref="CreateMessage"/> などから行う。</remarks>
    private QuickMessageEditItem(string label, string text, ObservableCollection<QuickMessageEditItem>? children)
    {
        Label = label;
        Text = text;
        Children = children;

        if (children is not null)
        {
            // グループは開いた状態が基本で、閉じるのはユーザーが閉じたときだけ。中に文が入ったら (追加・ドロップ)開く
            IsExpanded = true;
            children.CollectionChanged += (_, e) =>
            {
                if (e.Action is NotifyCollectionChangedAction.Add)
                {
                    IsExpanded = true;
                }
            };
        }
    }

    /// <summary>表示名</summary>
    /// <remarks>文は、省略すると文の 1 行目。グループは、グループ名。</remarks>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayName))]
    public partial string Label { get; set; }

    /// <summary>入力欄に入れる文 (文のとき)</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayName))]
    public partial string Text { get; set; }

    /// <summary>グループが開いているか</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Glyph))]
    public partial bool IsExpanded { get; set; }

    /// <summary>グループの中の文</summary>
    /// <remarks>グループのときだけ持ち、文は null。</remarks>
    public ObservableCollection<QuickMessageEditItem>? Children { get; }

    /// <summary>グループか</summary>
    public bool IsGroup => Children is not null;

    /// <summary>種類のアイコン</summary>
    /// <remarks>グループは開閉で変える。</remarks>
    public string Glyph => IsGroup ? (IsExpanded ? "" : "") : "";

    /// <summary>一覧に出す名前</summary>
    /// <remarks>表示名、無ければ文の 1 行目。どちらも無ければ「(未入力)」。</remarks>
    public string DisplayName
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(Label))
            {
                return Label.Trim();
            }
            var firstLine = IsGroup ? null : Text.Split('\n').FirstOrDefault(line => !string.IsNullOrWhiteSpace(line));
            return firstLine is null ? "(未入力)" : firstLine.Trim();
        }
    }

    /// <summary>新しい文を作る</summary>
    /// <returns>新しい文の項目</returns>
    public static QuickMessageEditItem CreateMessage() => new(string.Empty, string.Empty, null);

    /// <summary>新しいグループを作る</summary>
    /// <returns>新しいグループの項目</returns>
    public static QuickMessageEditItem CreateGroup() => new(string.Empty, string.Empty, []);

    /// <summary>保存されている内容から編集用の項目を作る</summary>
    /// <param name="message">保存されている文またはグループ</param>
    /// <returns>編集用の項目</returns>
    public static QuickMessageEditItem From(CliQuickMessage message)
        => message.Items is { } children
            ? new QuickMessageEditItem(message.Label ?? string.Empty, string.Empty, new ObservableCollection<QuickMessageEditItem>(children.Select(From)))
            : new QuickMessageEditItem(message.Label ?? string.Empty, message.Text ?? string.Empty, null);

    /// <summary>保存用の内容に変換する</summary>
    /// <returns>保存用の文またはグループ</returns>
    public CliQuickMessage ToMessage()
        => Children is null
            ? new CliQuickMessage { Label = Label, Text = Text }
            : new CliQuickMessage { Label = Label, Items = [.. Children.Select(child => child.ToMessage())] };
}
