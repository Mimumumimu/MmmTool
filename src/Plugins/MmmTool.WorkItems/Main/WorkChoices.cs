using MmmTool.CodeMaster.Core;
using MmmTool.WorkItems.Core;

namespace MmmTool.WorkItems.Main;

/// <summary>表の選択欄 (優先度・状態・進捗度)の選択肢 1 つ</summary>
/// <param name="CodeNo">コード番号 (作業の行に保存する値)</param>
/// <param name="Text">画面に出す表記</param>
public sealed record WorkChoice(int CodeNo, string Text);

/// <summary>表の選択欄の選択肢の一覧 (コードマスタから作る)</summary>
/// <remarks>コードマスタが変わったときだけ作り直す。作業の行は、同じ一覧を共有して、コード番号から位置と表記を引く。</remarks>
public sealed class WorkChoices
{
    /// <summary>コードブックから、選択肢の一覧を作る</summary>
    /// <param name="book">コードブック</param>
    public WorkChoices(CodeBook book)
    {
        Book = book;
        Priorities = ToChoices(book, WorkCodes.PriorityCategory);
        Statuses = ToChoices(book, WorkCodes.StatusCategory);
        Progresses = ToChoices(book, WorkCodes.ProgressCategory);
    }

    /// <summary>元のコードブック</summary>
    public CodeBook Book { get; }

    /// <summary>優先度の選択肢 (表示順)</summary>
    public IReadOnlyList<WorkChoice> Priorities { get; }

    /// <summary>状態の選択肢 (表示順)</summary>
    public IReadOnlyList<WorkChoice> Statuses { get; }

    /// <summary>進捗度の選択肢 (表示順)</summary>
    public IReadOnlyList<WorkChoice> Progresses { get; }

    /// <summary>選択肢の中での、コード番号の位置を返す</summary>
    /// <param name="choices">選択肢</param>
    /// <param name="codeNo">コード番号</param>
    /// <returns>位置。選択肢から外れているコード (論理削除)は -1</returns>
    public static int IndexOf(IReadOnlyList<WorkChoice> choices, int codeNo)
    {
        for (var i = 0; i < choices.Count; i++)
        {
            if (choices[i].CodeNo == codeNo)
            {
                return i;
            }
        }
        return -1;
    }

    /// <summary>区分の選択肢を、表の選択肢にする</summary>
    /// <param name="book">コードブック</param>
    /// <param name="categoryNo">区分番号</param>
    /// <returns>選択肢 (表示順)</returns>
    private static List<WorkChoice> ToChoices(CodeBook book, int categoryNo)
        => [.. WorkCodes.Options(book, categoryNo).Select(e => new WorkChoice(e.CodeNo, WorkCodes.Display(e)))];
}
