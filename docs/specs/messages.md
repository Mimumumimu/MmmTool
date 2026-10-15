# メッセージ送信

宛先 (ほかのユーザーの送信先を含む)を選んで、短いメッセージを送る機能。DB モード (サーバーモード)だけで使う。MmmTool は送信の依頼を DB に書くだけで、実際に外部 (ntfy・Discord)へ送るのは MmmBatch ([mmmbatch.md](mmmbatch.md))。

## 位置づけ
- MmmTool に外部へ接続するコードは入れない。画面が書くのは `MessageSendRequest` の行 (表の定義は [database.md](database.md))だけで、MmmBatch が数秒おきにその行を読んで送る ([mmmbatch.md](mmmbatch.md) の「メッセージの送信」)
- 依頼を DB に書くだけなので、MmmTool の側では、送れたかどうかは分からない。画面に出すのは「依頼したこと」まで
- 送信の依頼の行は、送信先の値 (ntfy のトピック名・Discord の Webhook の URL)を持たない。持つのは送信先の番号 (`ChannelId`)だけで、値は MmmBatch が送るときに `NotificationChannel` から読む。MmmTool の宛先の一覧も、値を読まない (値は秘密のため)

## プロジェクト
| 場所 | 中身 |
| --- | --- |
| `src/NotificationChannels/MmmTool.NotificationChannels.Core` | 送信先 (`NotificationChannel`・区分・登録名と値の規則・`INotificationChannelRepository`)。リマインダーとメッセージで共有する (機能どうしは参照しないため、どちらの Core にも置かない) |
| `src/Plugins/MmmTool.Messages.Core` | 送信依頼 (`MessageSendRequest`・状態)・入力の上限 (`MessageLimits`)・保存先のインターフェース (依頼の保存と宛先の読み出し)。保存先が DB でないとき (ローカルモード)は「使えない」実装を登録し、`IsAvailable` が false になる |
| `src/Plugins/MmmTool.Messages` | 画面 (`Main/`)・トレイのメニュー・DI 登録の入口 (`MessagesPlugin`)。`MmmTool.Data*` は参照しない |
| `src/Data/MmmTool.Data` / `MmmTool.Data.SqlServer` | 行のクラス・Repository の実装 (他の機能と同じ。[../architecture.md](../architecture.md) の「DB のコードの置き場所」) |

- `NotificationChannel` の登録の画面 (一覧・編集・設定ページの「送信先」)は、今までどおりリマインダーの機能 (`MmmTool.Reminders`)に置く。移すのは型と Repository のインターフェースだけ

## 画面
- 入口は 2 つ。サイドバーのページ「メッセージ」(ページの見出しは「メッセージ」)と、トレイのメニュー「メッセージ送信」(普通のウィンドウ。アプリ内で 1 枚だけ。開いていれば前面に出す。位置と大きさは次回に復元する)。入力欄は 1 つの部品 (`MessageSendControl`)を両方で共有する
- 項目は、上から次の順
  - 宛先 (必須): 選ぶ。表示は `ユーザー名 - 登録名 (ntfy)` / `(Discord)`
  - タイトル (任意): 最大 100 字。薄い文字は「省略できます」
  - 本文 (必須): 最大 1,000 字。複数行。ページでもウィンドウでも、余った高さまで伸びる (ページでは最小 240、ウィンドウでは最小 120)
  - 送信ボタン (強調色): 宛先と本文があるときだけ押せる
  - 結果の InfoBar: 成功と失敗を出す
- タイトルと本文は、フォーカスが来たら IME をオンにする (日本語の入力が多いため)
- 文字数は UTF-16 の 1 単位で数え、上限は DB の列の長さ ([database.md](database.md))と同じ (`MessageLimits`)
- 文言は仮 (宛先・タイトル・本文・送信・案内の文)

## 宛先の一覧
- 削除されていない送信先すべて (自分のもの以外も含む)。使用者が削除されているものは出さない。使用者の番号順、送信先の番号順
- 自分以外の送信先にも送れる (リマインダーの宛先とは違い、目的が「ほかの人に一言送る」ことのため)
- 初期の選択は、選んでいるもの、なければ前回送った宛先、それも無ければ先頭。前回の宛先は設定ストアの `Messages.LastChannelId`(整数。この PC の設定。無ければ 0)に、依頼を書けたときに保存する。保存できなかったときは、結果に添えて知らせ、続ける
- 宛先が 1 件も無いときは、選ばず、送れない状態のまま、「送信先がありません。設定ページの「送信先」で登録してください。」の案内を出す (落とさない)
- 読めなかったとき (`DataFileException`)は、結果の InfoBar に出す

## 送るときの動き
- 押すと、依頼の行を書く (状態は送信待ち・試した回数は 0・送った人は今のユーザー)。タイトル・本文は、前後の空白を取る
- 書けたら、本文を空にし (タイトルと宛先は残す)、結果に「`<宛先>` に送信を依頼しました」と出す
- 書けなかったとき (`DataFileException`)は、本文を残し、理由を結果に出す
- 実際に届いたかどうかは、この画面では分からない。送信済み・失敗を MmmTool の画面に出さない (結果は MmmBatch の画面とログで見る)。MmmBatch が長く止まっていたあとの起動でも、送信待ちの行は古さにかかわらず送る

## DB モードだけ
- `FeatureInfo.AvailableWhen` ([SDK の architecture.md](../../external/MmmSdk/docs/architecture.md))で、依頼の保存先が使えない (`IsAvailable` が false = ローカルモード)ときは、機能そのものを無いものとして扱う。サイドバー・トレイ・設定ページの「機能」の一覧のどれにも出ない
- 機能のオン・オフは既定でオン。設定ファイルに `Feature.Messages.Enabled` が残っていても、ローカルモードでは効かない ([settings.md](settings.md))
- 設定ページの「送信先」(送信先の登録の画面を開く)も、DB モードのときだけ出す

## 決めたこと
- 依頼の表に、失敗の理由の列を持たない (理由は MmmBatch のログに残す。経過の記録は、状態を持つ表の役割ではないため)
- 宛先を自分の送信先に限らない (上のとおり)
- 依頼の行は、同じ内容を続けて送っても別の行にする (一意制約を付けない。1 回の送信の操作が 1 行)
