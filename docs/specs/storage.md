# 保存

## 方針
- 保存は JSON。場所は `AppContext.BaseDirectory/Data/*.json`。手で修正するときは、アプリを閉じてから行う (起動中はメモリ上の内容で上書きされることがある)
- `Data` の下には、JSON のほかに、アプリが書くフォルダとして `Logs`(エラーのログ)と `WebView2`(ターミナルの画面のキャッシュ)がある。EXE の横には `Data` 以外の書き込み先を作らない ([decisions/0014-output-folders.md](../decisions/0014-output-folders.md))
- 保存先は将来 SQL Server / DynamoDB などに替える可能性がある。Repository のインターフェース (非同期)と DI で差し替える
- JSON はソース生成 (`JsonSerializerContext`)で読み書きする。Context は機能ごと (`CliAssistJsonContext` / `LinkJsonContext` / `ReminderJsonContext`)。書式 (インデント・日本語非エスケープ・コメント可・大文字小文字を区別しない等)は SDK の `ReadableJsonOptions.Create()` に 1 か所だけ書く
- JSON の読み込みはプロパティ名の大文字小文字を区別しない (アプリの全機能に適用)
- JSON の読み書きの仕組み・壊れたファイルの扱い・汎用設定ストアは SDK 側の設計 (`external/MmmSdk/docs/storage.md`)。ここではアプリ側の使い方だけを書く

## ファイル
| ファイル | 内容 | 備考 |
| --- | --- | --- |
| `Data/CliCommands.json` | CLI補助の定型コマンドと、動かす環境 (`environment`: `windows` / `wsl`) | ローカル専用。ID なしの入れ子 JSON。無ければ、初期設定ダイアログ (使うツール・環境)を出してから既定を生成 |
| `Data/CliSettings.json` | CLI補助の最後の作業ディレクトリ・ディレクトリ履歴 (20 件) | ローカル専用 |
| `Data/Links.json` | リンクの構成 | ローカル専用。ID なしの入れ子。無ければ空で生成 |
| `Data/Reminders.json` | リマインダー | DB に替える可能性がある (ID あり) |
| `Data/ReminderStates.json` | リマインダーの日ごとの対応状態 | 同上 |
| `Data/AppSettings.json` | 設定 (キー → 値の辞書) | SDK の設定ストア。スヌーズ間隔・ウィンドウ位置など |

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
- 送信履歴 (50 件)は、プライバシーと実用性の理由で保存しない。`SendHistory`(Core)として `CliAssistViewModel` がメモリ上だけに持つ (起動中のみ。どのフォルダ・チャットかは持たない)
- 添付のうち、元がファイルではないもの (貼り付けた画像など)だけを `%TEMP%\MmmTool\session_日時\` に連番で保存する。ドロップ・貼り付けしたディスク上のファイルはコピーせず、元のパスを送る ([cli-assist.md](cli-assist.md))
