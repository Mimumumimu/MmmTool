using CommunityToolkit.Mvvm.Input;
using MmmSdk.Core.Components.Notifications;
using MmmSdk.WinUI.Components.Notifications;
using MmmTool.Core.Reminders;
using MmmTool.Features.Reminders;

namespace MmmTool.Features.Debugging;

/// <summary>DEBUG ページの ViewModel（デバッグビルドだけで使う）</summary>
/// <param name="notifications">通知ダイアログの表示</param>
/// <param name="reminderDialogs">リマインダーの入力・一覧画面を開く</param>
/// <param name="reminders">リマインダーの読み書き</param>
public sealed partial class DebugViewModel(
    INotificationDialogService notifications,
    IReminderDialogService reminderDialogs,
    ReminderService reminders)
{
    /// <summary>通知ダイアログを試しに表示した回数</summary>
    private int _count;

    /// <summary>本文のクリックで閉じられた回数（コールバックの確認用）</summary>
    private int _clickedCount;

    /// <summary>通知ダイアログを表示する</summary>
    /// <remarks>テキストだけの項目・リンク・折り返す長文を混ぜて、見た目と明滅を確かめる。押すたびに内容を差し替える（偶数回目は項目を多くしてスクロールも確かめる）。</remarks>
    [RelayCommand]
    private void ShowNotification()
    {
        _count++;
        notifications.Show($"テスト通知 {_count}（クリックで閉じた回数 {_clickedCount}）",
        [
            new NotificationItem("テキストだけの項目"),
            new NotificationItem("リンクの項目（既定のブラウザーで開く）", "https://example.com"),
            new NotificationItem("フォルダのリンク（%TEMP%）", "%TEMP%"),
            new NotificationItem("長い項目：折り返しの確認のため、少し長めの文章を入れています。幅 400 に収まらない場合は次の行へ折り返されます。"),
            .. Enumerable.Range(1, _count % 2 == 0 ? 12 : 0).Select(n => new NotificationItem($"スクロール確認用の項目 {n}")),
        ],
        onClicked: () => _clickedCount++);
    }

    /// <summary>リマインダー入力画面を新規で開く</summary>
    /// <returns>入力画面が閉じるまでの待機を表すタスク</returns>
    /// <remarks>保存した内容は通知ダイアログで確かめる。</remarks>
    [RelayCommand]
    private async Task NewReminderAsync() => ShowSaved(await reminderDialogs.ShowInputAsync(null));

    /// <summary>リマインダー入力画面を、最初の 1 件（論理削除済みも含む）の編集で開く</summary>
    /// <returns>入力画面が閉じるまでの待機を表すタスク</returns>
    /// <remarks>1 件も無ければ、その旨を通知ダイアログで知らせる。</remarks>
    [RelayCommand]
    private async Task EditFirstReminderAsync()
    {
        var all = await reminders.GetRemindersAsync(includeDeleted: true);
        if (all.Count == 0)
        {
            notifications.Show("DEBUG", "編集できるリマインダーがありません。先に新規で登録してください。");
            return;
        }
        ShowSaved(await reminderDialogs.ShowInputAsync(all[0]));
    }

    /// <summary>リマインダー一覧画面を開く</summary>
    /// <returns>一覧画面が閉じるまでの待機を表すタスク</returns>
    [RelayCommand]
    private Task ShowReminderListAsync() => reminderDialogs.ShowListAsync();

    /// <summary>保存した内容を通知ダイアログに出す（キャンセルなら何もしない）</summary>
    /// <param name="saved">保存したリマインダー。キャンセルなら null</param>
    private void ShowSaved(Reminder? saved)
    {
        if (saved is null) return;
        var when = ReminderDates.IsWeekdaySpecified(saved.Date)
            ? $"曜日指定 {ReminderDates.DescribeWeekdays(saved.Weekdays)}"
            : $"日付指定 {saved.Date}";
        notifications.Show("DEBUG：保存しました",
        [
            new NotificationItem($"No {saved.No} / 削除 {saved.IsDeleted}"),
            new NotificationItem($"{when} / 時刻 {saved.Time:0000}"),
            new NotificationItem($"件名 {saved.Title}"),
            new NotificationItem($"備考 {saved.Note ?? "(なし)"}"),
            new NotificationItem($"リンク {saved.Link ?? "(なし)"}", saved.Link),
        ]);
    }
}
