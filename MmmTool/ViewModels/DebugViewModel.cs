using CommunityToolkit.Mvvm.Input;
using MmmSdk.Core.Services;
using MmmSdk.WinUI.Services;
using MmmTool.Core.Services;
using MmmTool.Services;

namespace MmmTool.ViewModels;

/// <summary>DEBUG ページの ViewModel（デバッグビルドだけで使う）</summary>
public sealed partial class DebugViewModel(INotificationDialogService notifications)
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
}
