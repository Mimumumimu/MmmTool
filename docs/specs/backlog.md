# Backlog

Backlog の共有ファイル (ファイル画面の URL で示すフォルダー)の一覧を取得し、ファイルをダウンロードする機能の仕様。

## 概要

- 出るのは、サイドバーのクリップボード転送の下のページ 1 つと、設定ページの API キー欄
- 場所は `src/Plugins/MmmTool.Backlog/` で、Core 側は `src/Plugins/MmmTool.Backlog.Core/`
- 設定ページでオン・オフできる ([settings.md](settings.md))
- 追加・削除は Backlog の公開 API で行えないので、ブラウザーで Backlog の画面を開いて手で行う (「ブラウザで開く」)

## データ

- 専用のファイルは作らず、保存するのは次の 2 つだけ

  | 内容 | 保存先 | キー・名前 |
  | --- | --- | --- |
  | 連携用パス (URL) | 設定ストア (`Data/AppSettings.json`) | `Backlog.Url` |
  | API キー | Windows の資格情報マネージャー (汎用資格情報) | ターゲット名・ユーザー名とも `MmmTool.Backlog` |

- **URL は履歴を持たず、最後の 1 件を上書きする**
  - 一覧を取得できたときに保存し、次にページを作るときに入力欄へ戻す
  - 入力のたびに保存しない (入力途中の URL が残るため)
- **API キーは設定ファイルに書かない**
  - SDK の `ISecretStore`(実装は `CredentialSecretStore`)で、資格情報マネージャーに保存する ([SDK の storage.md](../../external/MmmSdk/docs/storage.md))
  - 保存先は今の Windows ユーザーの、この PC の中だけで、ほかの PC に同期しない
  - Data フォルダーを人に渡しても、キーは渡らない
  - 資格情報マネージャーの「Windows 資格情報」→「汎用資格情報」に `MmmTool.Backlog` として見える
  - 入力欄を空にして保存すると、項目も消える
  - アンインストール後に残ったものは、資格情報マネージャーから手で消す
  - 設定ファイルに平文で書く案は採らない (ユーザーの決定)
    - 公開する可能性があり、Data フォルダーは EXE の横にあるため、フォルダーを人に渡す・バックアップに入れるだけでキーが漏れる
- 名前 (`BacklogSettingsService.ApiKeySecretName`)は、決めたら変えない (変えると、保存済みのキーを読めなくなる)

## 連携用パス (URL)の解析 (`BacklogLocationParser`。Core)

- 形は `https://{domain}/file/{projectKey}/{エンコード済みフォルダーパス}`
  - 分けた結果は `BacklogLocation`(ドメイン・プロジェクトキー・フォルダーパス)
  - `http` / `https` の絶対 URL でなければエラー
  - ドメインが `*.backlog.jp` / `*.backlog.com` / `*.backlogtool.com` でなければエラー (API キーを送る先なので、違う URL を貼ったときに、キーを別のサーバーへ送らないため)
  - パスを `/` で分け、先頭が `file`、次がプロジェクトキー、残りがフォルダーパス。この形でなければ「Backlog のファイル画面の URL ではありません」。
  - プロジェクトキー・フォルダーパスの各セグメントは、URL デコードして持つ (フォルダーパスは `/` でつなぎ、ルートは空)
- 予測できる失敗なので、例外にせず、結果の `Error`(画面に出す文)で返す

## 通信 (`BacklogClient`。Core)

- `HttpClient` はアプリ全体で 1 つ
  - `BacklogClient` を Singleton にし、自分で持つ
  - 接続は 10 分で張り直す
  - タイムアウトは 5 分
- 一覧: `GET https://{domain}/api/v2/projects/{projectKey}/files/metadata/{フォルダーパス}?apiKey={apiKey}`
  - フォルダーパスは、区切りの `/` も含めて URL エンコードする (`/` は `%2F` になり、先頭・末尾の `/` は無視する)
  - 応答は共有ファイルの配列
  - 種別がファイル (`file`)のものだけを一覧に出す (フォルダーは出さない)
  - プロパティ名の大文字小文字は区別しない (Backlog の camelCase に対応)
  - JSON は、保存ファイル用の設定 (SDK の `ReadableJsonOptions`)ではなく、Web の既定で読む専用の Context (`BacklogJsonContext`)で読む
    - 応答の形は Backlog が決めているため
- ダウンロード: `GET https://{domain}/api/v2/projects/{projectKey}/files/{sharedFileId}?apiKey={apiKey}`
  - 応答の先頭を受けたら、メモリに溜めず、保存先の隣の一時ファイル (`名前.download`)へ書きながら受け、終わったら置き換える (途中で失敗・取り消しされても、元のファイルを壊さず、一時ファイルも残さない)
- 通信は常に https (URL が `http` でも、API キーを平文で送らない)
- 失敗は `BacklogException`(メッセージはそのまま画面に出せる)で上げる。API キーはクエリに入るので、メッセージに URL を入れない。
  - 401・403: API キーが正しくないか、権限が無い
  - 404: プロジェクトまたはフォルダーが無い
  - 429: 要求が多すぎる
  - そのほかの失敗: HTTP の番号
  - 接続できない・時間切れ・応答が読めない: それぞれの文
- 保存先に書けないとき (ロック・権限など)は、`IOException` / `UnauthorizedAccessException` をそのまま上げ、ViewModel が受ける

## 画面 (`Main/BacklogPage` ＋ `BacklogViewModel` ＋ `BacklogFileItem`)

- 余白 24・タイトル「Backlog」
- 上から、進行 (ProgressBar)と結果・エラーの InfoBar、連携用パスのカード、ファイル一覧のカード (リンク・クリップボード転送のページと同じカードの作り)
- **連携用パスのカード**:
  - 入力欄 (見出し「連携用パス (URL)」)と、「一覧取得」 (強調色)・「ブラウザで開く」 (標準のボタン)を置く
  - 主な操作は「一覧取得」だけなので、ここだけ強調する
  - URL が空の間は、2 つのボタンを無効にする (案内文は出さない)
  - 「ブラウザで開く」: 入力した URL を既定のブラウザーで開く (`IPathOpener`)
    - `http` / `https` 以外は開かない
- **ファイル一覧のカード**:
  - 見出しに、最後に一覧を取得できたフォルダー (プロジェクト / フォルダー)と件数を出す
  - 入力欄の URL ではなく、取得したときの値を出す (入力中の URL とずれないため)
  - 行は、ファイルのアイコン・ファイル名・大きさ・更新日時・「ダウンロード」ボタンを並べる
    - ファイル名は、長いときは省略記号にして、ToolTip に全文を出す
    - 更新日時は、この PC のタイムゾーンで `yyyy/MM/dd HH:mm`
    - ファイル名以外は、2 次の文字色
  - まだ取得していないとき・ファイルが 1 件も無いときは、一覧の位置に短い案内を出す
- 取得に失敗したときは、前の一覧と見出しをそのまま残す (見出しと一覧が食い違わないため)
- 処理中 (`IsBusy`)は、入力欄とボタンを受け付けない。進行表示 (`IsProcessing`)は、通信・書き込みの間だけ出す。保存先を選んでもらっている間は出さない。
- 結果は、ダイアログではなくページ上部の InfoBar に出す (クリップボード転送と同じで、ダイアログは操作を止めるうえ、ほかの機能と見た目がそろわないため)
  - ダウンロードの成功は、保存先・大きさを成功の InfoBar に出し、失敗はエラーの InfoBar に出す
  - 次の操作を始めると、前の分は消える

## 操作

- **一覧取得**: URL を解析 → API キーを確認 → 一覧を取得 → 一覧と見出しを更新 → URL を保存
  - 解析に失敗したときは、理由をエラーの InfoBar に出す
- **ダウンロード**: API キーを確認 → 保存先のダイアログ (`IFilePickerService.PickSaveFileAsync`)→ ダウンロード → 完了を InfoBar に出す
  - 最初のファイル名は元の名前
  - 同じ名前があるときの上書きの確認は、保存先のダイアログが出す
  - キャンセルしたら通信しない (先に通信すると、キャンセルしたとき通信が無駄になるため)
- API キーは、押したときに読む (ページは使い回されるので、設定ページで登録・変更した直後でも、新しい値が使われるため)
- API キーが未登録なら、通信せずに「API キーが登録されていません。設定ページで登録してください。」をエラーの InfoBar に出す
- 資格情報マネージャーを読めなかったときは、その理由を出す
- 入力欄の下に注記を常には置かない (登録済みの人には毎回の余計な文になるため)

## 設定ページの項目 (`Settings/BacklogSettingsControl` ＋ `BacklogSettingsViewModel`)

- 「Backlog の API キー」と `PasswordBox` を置く
  - `PasswordBox` は、右端のボタンを押している間だけ見える
  - リマインダーの項目と同じ、左に名前・右に入力の横並び
- 保存済みのキーを入力欄に入れて開く
- 入力欄からフォーカスが外れたときに保存する (1 文字ごとに保存して、入力途中のキーを保管庫に書かないため)
- 前後の空白は除く (貼り付けたときに混ざりやすいため)
- 変わっていなければ保存しない。空なら登録を消す。
- 保管庫を読めない・書けないときは、画面の InfoBar に出して続ける (`SecretStoreException`)
- 機能がオフの間は、この項目も並べない

## SDK の部品

- アプリ固有でない部分は SDK にある:
  - `ISecretStore` / `CredentialSecretStore`: 秘密の保存 ([../../external/MmmSdk/docs/storage.md](../../external/MmmSdk/docs/storage.md))
  - `IPathOpener`: URL を既定のブラウザーで開く
  - `IFilePickerService.PickSaveFileAsync`: 保存先の選択 ([../../external/MmmSdk/docs/dialogs.md](../../external/MmmSdk/docs/dialogs.md))
  - `FileSizeFormatter`: 大きさの表記 (クリップボード転送と共有)
