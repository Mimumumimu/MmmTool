# 設定ページ

`Features/Settings/SettingsPage` ＋ `SettingsViewModel`。縦に項目を並べる `StackPanel`（項目はここへ足す）。メイン画面のサイドバー下部に置く。

## 項目
### リマインダーのスヌーズ間隔
- `NumberBox`（スピンは Inline・小さい刻み 1 / 大きい刻み 10）と双方向で、変わったら即保存する
- 保存は `ReminderSettingsService`（Core）経由で、設定ストアの `Reminder.SnoozeIntervalMinutes` に委譲する
- 範囲は 5〜999 分（定数と補正は `ReminderSettingsService` の `MinSnoozeInterval` / `MaxSnoozeInterval` / `ClampSnoozeInterval`）、既定は 15。範囲外・空は範囲内に補正して、画面にも反映する（`NumberBox` は空にすると NaN になる）
- 保存の失敗（`DataFileException`）は、画面の InfoBar に出して続ける（`SaveSnoozeIntervalAsync`）。想定外の失敗は、握りつぶさず安全網（ログ → ダイアログ → 終了）へ流す

## 今後
- Backlog 連携を実装するときに、API キーの欄を足す予定（今は置いていない）
