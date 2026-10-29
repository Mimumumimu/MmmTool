# 保存

保存の方針、`Data` の中のファイル、壊れたファイルの扱い、保存しないデータの仕様。

## 方針

- 保存は JSON で、場所は `AppContext.BaseDirectory/Data/*.json`
  - 手で修正するときは、アプリを閉じてから行う (起動中はメモリ上の内容で上書きされることがある)
  - 場所を EXE の横の `Data` にし、`%LOCALAPPDATA%` に置かない (ユーザーの決定)
    - アンインストール後も残骸がたまるため
    - フォルダごと持ち運べて、消すときも 1 フォルダで済む形を選んだ
  - その代わり、フォルダを別の場所へコピーするときに、移動先の `Data` を上書きしてデータが消えるおそれがある
    - 「`Data` を上書きしない」の 1 点だけを、配布物の説明書の先頭 (`README.txt`・`README.md`)とリリース本文の先頭に書く
    - `操作説明.html` には書かない (初回の注意とは役割が違う)
    - アプリの画面の中に出す案 (設定ページに `Data` の場所と「フォルダを開く」)は、ユーザーの判断で見送った
- `Data` の下には、JSON のほかに、アプリが書くフォルダとして `Logs` がある
  - `Logs`: エラーのログ
- EXE の横の `Cache` は、消しても作り直されるデータの置き場 (`AppInfo.CacheDirectory`)
  - `Cache\WebView2`: ターミナルとノートの画面のキャッシュ。アプリの中の WebView2 は、すべて同じ場所を使う。
  - `Cache\MessageCache.db`: メッセージの端末の控えで、SQLite のファイル。会話ごとに新しい順の 200 件 (仮)までを、ログインした人ごとに分けて持つ。ログアウトとアカウントの変更で、その人の分を消す。詳細は [messages.md](messages.md) の「端末の控えと更新」。
  - `Cache` を `Data` と分けるのは、`Data` を軽く保ち、バックアップや持ち運びの対象にキャッシュを入れないため
  - 消しても作り直されるデータは、種類ごとにフォルダを足さず、`Cache` の下に置く
- Web 版 (MmmToolWeb)の `Data` に保存するものは [web.md](web.md) の「保存するもの」
- EXE の横には `Data` と `Cache` 以外の書き込み先を作らない (理由は [../build-and-distribution.md](../build-and-distribution.md) の「EXE の横のフォルダと DLL」)
- DB に保存する機能 (リマインダー・作業管理・ノート)は、設定で選んだ保存先 (ローカルの JSON・SQLite、または SQL Server)に保存する ([database.md](database.md))
- どの保存先も、Repository のインターフェース (非同期)と DI で差し替える
- JSON のソース生成と書式の決まりは、`csharp-dotnet` スキルに従う。書式は SDK の `ReadableJsonOptions.Create()` にある。
  - Context は機能ごと (`CliAssistJsonContext` / `LinkJsonContext` / `ReminderJsonContext`)
- JSON の読み込みはプロパティ名の大文字小文字を区別しない (アプリの全機能に適用)
- JSON の読み書きの仕組み・壊れたファイルの扱い・汎用設定ストアは SDK 側の設計で、`external/MmmSdk/docs/storage.md` にある
  - ここではアプリ側の使い方だけを書く

## ファイル

| ファイル | 内容 | 備考 |
| --- | --- | --- |
| `Data/CliCommands.json` | CLI補助の定型コマンドと、動かす環境 (`environment`: `windows` / `wsl`) | ローカル専用。ID なしの入れ子 JSON。無ければ、初期設定ダイアログ (使うツール・環境)を出してから既定を生成 |
| `Data/CliSettings.json` | CLI補助の最後の作業ディレクトリ・ディレクトリ履歴 (20 件)・開いているタブと選んでいるタブ (次回の起動で戻す) | ローカル専用 |
| `Data/CliQuickMessages.json` | CLI補助の入力欄に出す、よく使う文 (文は `label` 省略可・`text`、グループは `label`・`items`) | ローカル専用。編集画面 (CLI補助)で書くか、手で書く。無ければ何も出さず、編集画面で保存したときに作る |
| `Data/CliFocusTriggers.json` | CLI補助で、ターミナルの出力に出たらターミナルへフォーカスを移す文字列 (`text`) | ローカル専用。編集画面 (CLI補助)で書くか、手で書く。無ければ何もせず、編集画面で保存したときに作る |
| `Data/Links.json` | リンクの構成 | ローカル専用。ID なしの入れ子。無ければ空で生成 |
| `Data/MmmTool.db` | 作業管理の案件・グループ・作業 (リンクは、各行の `Link` 列)・日ごとの記録・日報メモ (自分の分で、表 `WorkDayNote` の `UserId`・`Date`・`Note`)・コードマスタ (状態・進捗度・優先度の選択肢)・ノート (表 `Note` の期間・実施日 `EventDate`・タイトル・本文・重要度) | SQLite で、ローカルモードの保存先。DB モードでは SQL Server に保存するので使わない。表は `WorkProject`・`WorkGroup`・`WorkItem`・`WorkRecord`・`WorkDayNote`・`CodeMaster`・`Note` の 7 つ。削除は論理削除で残る。無ければ最初に使うときに作り、版を上げる。壊れていたら `MmmTool.broken-yyyyMMdd-HHmmss.db` へ名前を変えて残し、作り直す。詳細は [work-items.md](work-items.md)・[database.md](database.md) の「SQLite の場合」・ノートは [notes.md](notes.md) |
| `Data/Reminders.json` | リマインダー | ローカルモードの保存先で、ID あり。DB モードでは使わない (SQL Server に保存する) |
| `Data/ReminderStates.json` | リマインダーの日ごとの対応状態 | 同上 |
| `Data/AppSettings.json` | 設定 (キー → 値の辞書) | SDK の設定ストア。キーは下の「AppSettings.json のキー」 |

## AppSettings.json のキー

| 項目 | キー | 値 |
| --- | --- | --- |
| 作業管理の列の表示と自分の担当だけ | `WorkItems.HiddenColumns`・`WorkItems.MineOnly` | |
| 作業管理の「過去の完了」の、起動時の表示 | `WorkItems.InitialPastCompleted` | 0 (表示)・1 (グループのみ表示)・2 (非表示)の整数。無ければ 1 |
| スケジュールの「完了」の、起動時の表示 | `WorkItems.InitialScheduleCompleted` | 0 (表示)・1 (グループのみ表示)・2 (非表示)の整数。無ければ 0 |
| 作業報告の「実績に時間を出す」 | `WorkItems.Report.ShowHours` | 真偽。既定は false |
| 作業報告の「進捗度表示」 | `WorkItems.Report.ShowProgress` | 真偽。既定は true |
| ノートに作業報告の枠を出すか | `Notes.Sources.WorkReport.Enabled` | 真偽。既定は false。詳細は [notes-sources.md](notes-sources.md) |
| 表示しない案件 | `WorkItems.HiddenProjects` | 案件の番号をカンマでつないだ文字列で、例は `1,5,7`。無ければ全部表示 |
| 読み上げの音量 | `Speech.VolumePercent` | 0〜100 の整数 |
| メインウィンドウの × ボタンの動き | `Shell.CloseButtonAction` | `"Tray"` (トレイに残る)・`"Exit"` (終了する)の文字列。無ければ・読めない値なら `"Tray"`。詳細は [settings.md](settings.md) |
| まとめて追加で前回選んだ「何分前に通知」 | `Reminder.Bulk.LeadMinutes` | 0・3・5・10・15 のどれか。無ければ 5。詳細は [reminders.md](reminders.md) の「まとめて追加」 |
| まとめて追加で前回選んだ「通知を読み上げる」 | `Reminder.Bulk.IsSpeak` | 真偽。既定は false |
| まとめて追加で前回選んだ送信先 | `Reminder.Bulk.ChannelIds` | 送信先の番号をカンマでつないだ文字列で、例は `1,5`。無ければ選ばない |
| Backlog の連携用パス | `Backlog.Url` | |
| サイドバーから外したウィンドウの大きさ | `WindowBounds.DetachedPage.<項目のキー>` | 位置と大きさの 4 つの値 (X・Y・Width・Height)。外したウィンドウは、大きさだけ使う。詳細は [tray-and-main.md](tray-and-main.md) |
| Web 版通知の送り手の公開鍵 | `WebPush.PublicKey` | MmmBatch の `Data/AppSettings.json` のキー。MmmBatch が起動時に作る。Web 版のサーバーの設定 `Web:PushPublicKey` に写す。詳細は [mmmbatch.md](mmmbatch.md) の「Web 版通知」 |
| Web 版通知の送り手の連絡先 | `WebPush.Subject` | MmmBatch の `Data/AppSettings.json` のキー。`mailto:` か `https://` で始まる文字列。無ければ `mailto:mmmtool@example.com` |
| DB の接続の設定 | `Database.Mode`・`Database.Server`・`Database.Name`・`Database.Authentication`・`Database.UserName`・`Database.TrustServerCertificate` | アプリごとの `Data/AppSettings.json` に持つ。MmmToolAdmin も、自分の `Data/AppSettings.json` に持ち、最初の起動で、接続の設定画面で入れる。パスワードは、ここに書かず、資格情報マネージャーに置く |
| スヌーズ間隔・ウィンドウ位置など | 各機能・SDK が持つキー | |

## ファイルに書かないもの

- Backlog の API キーは、Data フォルダーに書かず、Windows の資格情報マネージャー (汎用資格情報 `MmmTool.Backlog`)に保存する
  - 手で開いて見られる設定ファイルに、秘密を置かないため
  - 詳細は [backlog.md](backlog.md)
- DB のパスワードは、Data フォルダーに書かず、資格情報マネージャー (汎用資格情報 `MmmTool.Database`)に保存する
  - MmmTool と MmmToolAdmin は、同じ名前を使うので、同じ Windows ユーザーなら、どちらで入れたパスワードも、もう一方が使う。片方の接続の設定画面でパスワードを入れ直すと、もう一方も、新しいパスワードで動く。
  - MmmBatch は `MmmBatch.Database`、MmmToolWeb は `MmmToolWeb.Database` で、アプリごとに分ける。
- MmmBatch の Web 版通知の鍵の組 (公開鍵と秘密鍵)は、Data フォルダーに書かず、資格情報マネージャー (汎用資格情報 `MmmBatch.WebPushKeys`)に保存する
  - DB のパスワードと同じ置き場にする。詳細は [mmmbatch.md](mmmbatch.md) の「Web 版通知」

## 壊れたファイル (全 JSON 共通)

- 保存ファイルは手で修正する運用なので、書式の誤りで壊れることがある。壊れたものを読めないまま空で作り直すと、次の保存で元のデータが消える。手で直した内容は貴重なので、壊れていても消さず、退避して残す。
- JSON として読めないファイルは、同じフォルダに `名前.broken-yyyyMMdd-HHmmss.json` へ名前を変えて退避する
  - 同じ秒なら `-2` 以降を付ける
  - 自動では消さない
  - 読み込みは、値 null とメッセージを返す
- 呼ぶ側は「ファイルが無いとき」と同じに作り直す
  - Links は空
  - CliCommands は既定 (環境を選び直す)
  - CliSettings / AppSettings / リマインダーは空
- 0 バイト・空白だけ・`null` は、退避せず空として扱う
- ロック・権限などの IO エラーは退避できないので、従来どおり `DataFileException`。リンク・CLI設定・リマインダーは、元のデータを消さないよう保存を止める。
- Repository の `LoadAsync` は `DataLoadResult<T>` を返し、サービスは `RecoveryMessage` を持つ
  - リマインダーは、1 件単位の操作の保存先が `LoadError` / `RecoveryMessage` を持つ ([reminders.md](reminders.md))
- 知らせ方は各画面の InfoBar
  - CLI補助は、設定・定型コマンドの問題を 1 つにまとめて表示する
  - リンク編集は、初回表示のときに表示する。`LinkMenuService.RecoveryMessage` は起動時の読み込みの分も知らせるため、一度入ったら消さない。
  - 設定ストア・リマインダーは、メッセージを持つだけで、表示は画面を作るとき

## 保存データの扱い

- 送信履歴 (50 件)は、プライバシーと実用性の理由で保存しない
  - `SendHistory`(Core)として `CliSessionViewModel` がタブごとにメモリ上だけに持つ
  - タブを閉じる・アプリを終了すると消える
  - どのフォルダ・チャットかは持たない
- 添付のうち、元がファイルではないもの (貼り付けた画像など)だけを `%TEMP%\MmmTool\session_日時\` に連番で保存する
  - ドロップ・貼り付けしたディスク上のファイルはコピーせず、元のパスを送る
  - 詳細は [cli-assist.md](cli-assist.md)
