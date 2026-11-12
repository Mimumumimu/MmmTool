using MmmTool.Notes.Core;

namespace MmmTool.Notes;

/// <summary>ノートの入力ウィンドウを開く口</summary>
/// <remarks>ViewModel が画面の型を知らずに呼ぶ</remarks>
public interface INoteDialogService
{
    /// <summary>入力ウィンドウを親の上にモーダルで開く</summary>
    /// <param name="note">
    /// 編集するノート
    /// 新規なら null
    /// </param>
    /// <param name="defaultStartDate">新規のときの、開始日の初期値 (ページが表示している日)。</param>
    /// <returns>
    /// 保存したら true
    /// キャンセル・× なら false
    /// </returns>
    Task<bool> ShowEditAsync(Note? note, DateOnly defaultStartDate);
}
