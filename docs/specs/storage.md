# 保存

## 方針
- 保存は JSON。場所は `AppContext.BaseDirectory/Data/*.json`。手で修正するときは、アプリを閉じてから行う (起動中はメモリ上の内容で上書きされることがある)
- `Data` の下には、JSON のほかに、アプリが書くフォルダとして `Logs`(エラーのログ)と `WebView2`(ターミナルの画面のキャッシュ)がある。EXE の横には `Data` 以外の書き込み先を作らない (理由は [../build-and-distribution.md](../build-and-distribution.md) の「EXE の横のフォルダと DLL」)
- 保存先は将来 SQL Server / DynamoDB などに替える可能性がある。Repository のインターフェース (非同期)と DI で差し替える
- JSON はソース生成 (`JsonSerializerContext`)で読み書きする。Context は機能ごと (`CliAssistJsonContext` / `LinkJsonContext` / `ReminderJsonContext`)。書式 (インデント・日本語非エスケープ・コメント可・大文字小文字を区別しない等)は SDK の `ReadableJsonOptions.Create()` に 1 か所だけ書く
- JSON の読み込みはプロパティ名の大文字小文字を区別しない (アプリの全機能に適用)
- JSON の読み書きの仕組み・壊れたファイルの扱い・汎用設定ストアは SDK 側の設計 (`external/MmmSdk/docs/storage.md`)。ここではアプリ側の使い方だけを書く

## ファイル
| ファイル | 内容 | 備考 |
| --- | --- | --- |
| `Data/CliCommands.json` | CLI補助の定型コマンドと、動かす環境 (`environment`: `windows` / `wsl`) | ローカル専用。ID なしの入れ子 JSON。無ければ、初期設定ダイアログ (使うツール・環境)を出してから既定を生成 |
| `Data/CliSettings.json` | CLI補助の最後の作業ディレクトリ・ディレクトリ履歴 (20 件) | ローカル専用 |
| `Data/CliQuickMessages.json` | CLI補助の入力欄に出す、よく使う文 (`label` 省略可・`text`) | ローカル専用。編集画面 (CLI補助)で書くか、手で書く。無ければ何も出さず、編集画面で保存したときに作る |
| `Data/Links.json` | リンクの構成 | ローカル専用。ID なしの入れ子。無ければ空で生成 |
| `Data/Reminders.json` | リマインダー | DB に替える可能性がある (ID あり) |
| `Data/ReminderStates.json` | リマインダーの日ごとの対応状態 | 同上 |
| `Data/AppSettings.json` | 設定 (キー → 値の辞書) | SDK の設定ストア。スヌーズ間隔・ウィンドウ位置・Backlog の連携用パス (`Backlog.Url`)など |

## ファイルに書かないもの
- Backlog の API キーは、Data フォルダーに書かず、Windows の資格情報マネージャー (汎用資格情報 `MmmTool.Backlog`)に保存する。手で開いて見られる設定ファイルに、秘密を置かないため ([backlog.md](backlog.md))

## 壊れたファイル (全 JSON 共通)
- JSON として読めないファイルは、同じフォルダに `名前.broken-yyyyMMdd-HHmmss.json`(同じ秒なら `-2` 以降)へ名前を変えて退避し (自動では消さない)、値 null とメッセージが返る
- 呼ぶ側は「ファイルが無いとき」と同じに作り直す：Links は空、CliCommands は既定 (環境を選び直す)、CliSettings / AppSettings / リマインダーは空
- 0 バイト・空白だけ・`null` は、退避せず空として扱う
- ロック・権限などの IO エラーは退避できないので、従来どおり `DataFileException`。リンク・CLI設定・リマインダーは、元のデータを消さないよう保存を止める
- Repository の `LoadAsync` は `DataLoadResult<T>` を返し、サービスは `RecoveryMessage` を持つ (リマインダーは、1 件単位の操作の保存先が `LoadError` / `RecoveryMessage` を持つ。[reminders.md](reminders.md))
- 知らせ方は各画面の InfoBar
  - CLI補助は、設定・定型コマンドの問題を 1 つにまとめて表示する
  - リンク編集は、初回表示のときに表示する。`LinkMenuService.RecoveryMessage` は起動時の読み込みの分も知らせるため、一度入ったら消さない
  - 設定ストア・リマインダーは、メッセージを持つだけで、表示は画面を作るとき

## 保存データの扱い
- 送信履歴 (50 件)は、プライバシーと実用性の理由で保存しない。`SendHistory`(Core)として `CliSessionViewModel` がタブごとにメモリ上だけに持つ (タブを閉じる・アプリを終了すると消える。どのフォルダ・チャットかは持たない)
- 添付のうち、元がファイルではないもの (貼り付けた画像など)だけを `%TEMP%\MmmTool\session_日時\` に連番で保存する。ドロップ・貼り付けしたディスク上のファイルはコピーせず、元のパスを送る ([cli-assist.md](cli-assist.md))

## 決定の理由

### 壊れた保存ファイルは退避して作り直す
- 保存ファイル (JSON)は、手で修正する運用なので、書式の誤りで壊れることがある。壊れたファイルを読めないまま起動時に空で作り直すと、次の保存で元のデータが消える
- そのため、JSON として読めないファイルは、同じフォルダに `名前.broken-yyyyMMdd-HHmmss.json` へ名前を変えて退避し (自動では消さない)、画面でユーザーに知らせ、呼ぶ側は「ファイルが無いとき」と同じに作り直す。手で修正した内容は貴重なので、壊れていても消さずに残す
- 0 バイト・空白だけ・`null` は、壊れていないとみなして退避しない
- ロック・権限などの IO エラーは退避できないので、保存を止める (元のデータを消さない)。読めるようになってから、アプリを再起動して使う
- 仕組みは SDK の `docs/storage.md`

### EXE の横に書き込み先を増やさない
`Data` の下に `Logs` と `WebView2` を置く理由は [../build-and-distribution.md](../build-and-distribution.md) の「EXE の横のフォルダと DLL」。
