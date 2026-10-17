using MmmTool.Notes.Core;

namespace MmmTool.Notes;

/// <summary>ノートの入力ウィンドウを開く口 (ViewModel が、画面の型を知らずに呼ぶ)</summary>
public interface INoteDialogService
{
    /// <summary>入力ウィンドウを、親の上にモーダルで開く (新規の登録・編集)</summary>
    /// <param name="note">編集するノート。新規なら null</param>
    /// <param name="defaultStartDate">新規のときの、開始日の初期値 (ページが表示している日)</param>
    /// <returns>保存したら true。キャンセル・× なら false</returns>
    Task<bool> ShowEditAsync(Note? note, DateOnly defaultStartDate);
}
