namespace MmmTool.CodeMaster.Core;

/// <summary>画面から名前を編集できるコードマスタの区分 1 つ (機能が登録する)</summary>
/// <remarks>
/// 設定ページの「選択肢」は、登録されたものを並べるだけで、特定の機能を知らない。
/// 編集できるのは名前だけ。数値・表示順・選択肢の追加と論理削除は、画面から行わない。
/// </remarks>
public sealed record CodeCategoryEditor
{
    /// <summary>機能番号 (<see cref="CodeFeatures"/>)</summary>
    public required int FeatureNo { get; init; }

    /// <summary>区分番号 (機能が決める)</summary>
    public required int CategoryNo { get; init; }

    /// <summary>設定ページに出す題名 (例: 進捗度の名前)</summary>
    public required string Title { get; init; }

    /// <summary>設定ページに出す説明</summary>
    public required string Description { get; init; }

    /// <summary>機能のキー。機能がオフの間は、設定ページに出さない。どの機能にも属さないなら null</summary>
    public string? FeatureKey { get; init; }

    /// <summary>編集画面で、名前の前に固定で出す表記 (例: 進捗度は <c>70%</c>)</summary>
    public Func<CodeMasterEntry, string> Label { get; init; } = entry => entry.CodeNo.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>名前の入力を確かめる。入力できない理由を画面に出せる文で返す。問題が無ければ null</summary>
    public Func<string, string?> ValidateName { get; init; } = _ => null;

    /// <summary>保存したあとに、機能が行う処理で、画面に出している選択肢の読み直しなど。UI スレッドから呼ぶ</summary>
    public Func<Task> AfterSavedAsync { get; init; } = () => Task.CompletedTask;
}
