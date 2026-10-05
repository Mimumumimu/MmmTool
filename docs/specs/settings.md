# 設定ページ

`Features/Settings/SettingsPage` ＋ `SettingsViewModel`。メイン画面のサイドバー下部に置く。

## 構成 (機能ごとの部品を並べる)
- 設定の項目は、各機能が `AddSettingsSection<TControl>()`(`Shell/ShellServiceCollectionExtensions`。サイドバーの `AddNavigationPage` と同じ形)で、設定の部品 (`UserControl`)を登録する。設定ページは、登録順に縦に並べるだけで、特定の機能を知らない。機能を足すときに触るのは、その機能のフォルダと `App` の 1 行だけ
- 部品は設定ページを開くときに DI から作る (Transient)。値の読み書きと画面の状態は、その機能の ViewModel・サービスが持つ (部品の見た目は設定ページと同じ縦並び)
- `SettingsViewModel` はページ全体のことだけ：タイトルと、設定ファイルを読めなかったとき (`ISettingsStore.LoadError`)の InfoBar。読めなかったときは、各部品が、自分のサービスの `IsReadOnly` で入力欄を無効にする (元のファイルを上書きで消さないため)

## 機能のオン・オフ
- ページの先頭に、機能の一覧を出す (見出しは付けない。リマインダーの項目と同じ、左に名前・右に操作の横並び)。オフにできる機能 (`AddFeature` で登録したもの。今は CLI補助)を、登録順に `ToggleSwitch`(オン / オフ)で並べる。リンク・リマインダーはオフにできないので出さない。一覧は `FeatureService.Features` から作るだけで、特定の機能を知らない (`SettingsViewModel` ＋ `FeatureItem`)。設定の部品は、その機能がオン・オフされたときだけ並べ直す
- 切り替えはすぐ反映する (再起動は要らない)。サイドバー・トレイメニュー・設定ページの部品が、`FeatureService.Changed` で変わる。オフにした機能のページは捨て (`PageProvider.EvictDisabledPages`)、表示中なら先頭のページへ移る。オンにしたときは、その機能の起動時の準備を実行する
- 状態は設定ストアの `Feature.<キー>.Enabled`(bool)。保存が無い機能はオン
- CLI補助を、ターミナルが動いている間にオフにするときは、確認ダイアログを出す (「実行中のターミナルと、その中の作業が終了します」。`CliAssistDisableConfirmation`)。ターミナルが動いていなければ確認しない。取りやめると、スイッチは元に戻る
- 設定ファイルを読めなかったとき (`IsReadOnly`)は、スイッチを無効にする。保存できなかったとき (読み込めない・`DataFileException`)は InfoBar で知らせ、スイッチを元に戻す
- 仕組みと決めた理由は [../decisions/0018-feature-toggle.md](../decisions/0018-feature-toggle.md)

## 項目
### リマインダーのスヌーズ間隔 (`Features/Reminders/ReminderSettingsControl` ＋ `ReminderSettingsViewModel`)
- `NumberBox`(スピンは Inline・小さい刻み 1 / 大きい刻み 10)と双方向で、変わったら即保存する
- 保存は `ReminderSettingsService`(Core)経由で、設定ストアの `Reminder.SnoozeIntervalMinutes` に委譲する
- 範囲は 5〜999 分 (定数と補正は `ReminderSettingsService` の `MinSnoozeInterval` / `MaxSnoozeInterval` / `ClampSnoozeInterval`)、既定は 15。範囲外・空は範囲内に補正して、画面にも反映する (`NumberBox` は空にすると NaN になる)
- 保存の失敗 (`DataFileException`)は、画面の InfoBar に出して続ける (`SaveSnoozeIntervalAsync`)。想定外の失敗は、握りつぶさず安全網 (ログ → ダイアログ → 終了)へ流す
