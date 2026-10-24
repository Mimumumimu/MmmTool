using MmmTool.CodeMaster.Core;

namespace MmmTool.WorkItems.Core;

/// <summary>作業管理が使うコードマスタの、機能番号・区分番号と、表示の組み立て</summary>
/// <remarks>
/// 区分番号は台帳 (変えない)。状態と優先度のコード番号は <see cref="WorkItemStatus"/>・<see cref="WorkItemPriority"/> の値と同じ。
/// 進捗度のコード番号は選択肢の識別子で、%の値は数値 (<see cref="CodeMasterEntry.NumericValue"/>)が持つ。
/// </remarks>
public static class WorkCodes
{
    /// <summary>機能番号 (作業管理)</summary>
    public const int FeatureNo = CodeFeatures.WorkItems;

    /// <summary>区分番号: 状態</summary>
    public const int StatusCategory = 1;

    /// <summary>区分番号: 進捗度</summary>
    public const int ProgressCategory = 2;

    /// <summary>区分番号: 優先度</summary>
    public const int PriorityCategory = 3;

    /// <summary>進捗度の既定のコード番号 (0%)</summary>
    public const int DefaultProgressCodeNo = 1;

    /// <summary>区分の選択肢を返す</summary>
    /// <param name="book">コードブック</param>
    /// <param name="categoryNo">区分番号</param>
    /// <returns>論理削除されていない件 (表示順)</returns>
    public static IReadOnlyList<CodeMasterEntry> Options(CodeBook book, int categoryNo) => book.Options(FeatureNo, categoryNo);

    /// <summary>コードの画面に出す表記を返す</summary>
    /// <param name="book">コードブック</param>
    /// <param name="categoryNo">区分番号</param>
    /// <param name="codeNo">コード番号</param>
    /// <returns>進捗度は <c>70% (作成完了)</c> / <c>0%</c>、ほかは名前。コードが無ければ、コード番号の文字</returns>
    public static string Display(CodeBook book, int categoryNo, int codeNo)
        => book.Find(FeatureNo, categoryNo, codeNo) is { } entry ? Display(entry) : codeNo.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>コードの 1 件の画面に出す表記を返す</summary>
    /// <param name="entry">コードの 1 件</param>
    /// <returns>進捗度は <c>70% (作成完了)</c> / <c>0%</c>、ほかは名前</returns>
    public static string Display(CodeMasterEntry entry)
        => entry.CategoryNo == ProgressCategory ? FormatProgress(entry.NumericValue, entry.Name) : entry.Name;

    /// <summary>進捗度の % の値と名前から、表記を作る</summary>
    /// <param name="percent">%の値</param>
    /// <param name="name">名前 (空でもよい)</param>
    /// <returns>名前があれば <c>70% (作成完了)</c>、無ければ <c>60%</c></returns>
    public static string FormatProgress(int percent, string name)
        => name.Length > 0 ? $"{percent}% ({name})" : $"{percent}%";

    /// <summary>計算した進捗度 (%)の表記を返す</summary>
    /// <param name="book">コードブック</param>
    /// <param name="percent">%の値 (グループの計算値など)</param>
    /// <returns>同じ %の選択肢があり名前があれば <c>70% (作成完了)</c>。無ければ <c>73%</c></returns>
    public static string DisplayProgressPercent(CodeBook book, int percent)
        => FormatProgress(percent, Options(book, ProgressCategory).FirstOrDefault(e => e.NumericValue == percent && e.Name.Length > 0)?.Name ?? "");

    /// <summary>進捗度のコード番号を、%の値にする</summary>
    /// <param name="book">コードブック</param>
    /// <param name="codeNo">進捗度のコード番号</param>
    /// <returns>%の値。コードが無ければ 0</returns>
    public static int ProgressPercent(CodeBook book, int codeNo) => book.Find(FeatureNo, ProgressCategory, codeNo)?.NumericValue ?? 0;

    /// <summary>進捗度の名前を、画面から編集するための区分を作る</summary>
    /// <param name="featureKey">機能のキー。機能がオフの間は、編集の入口を出さない。どの機能にも属さないなら null</param>
    /// <param name="afterSavedAsync">保存したあとに行う処理で、開いている画面の表記の読み直しなど。無ければ null</param>
    /// <returns>進捗度の名前の編集の区分</returns>
    /// <remarks>名前に % は入れられない。画面の表記は、数値から % を組み立てるため。MmmTool の設定ページと、管理ツールが、同じ定義を使う。</remarks>
    public static CodeCategoryEditor ProgressNameEditor(string? featureKey, Func<Task>? afterSavedAsync = null) => new()
    {
        FeatureNo = FeatureNo,
        CategoryNo = ProgressCategory,
        Title = "進捗度の名前",
        Description = "進捗度の後ろに ( ) で添える名前を変更します",
        FeatureKey = featureKey,
        Label = entry => $"{entry.NumericValue}%",
        ValidateName = name => name.Contains('%') || name.Contains('％') ? "名前に % は入れられません。" : null,
        AfterSavedAsync = afterSavedAsync ?? (() => Task.CompletedTask),
    };
}
