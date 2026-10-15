# 未実装・残りの作業

これからやることだけを書く。実装したら消し、説明は該当の `docs/` に移す。実装済みの機能は `docs/specs/` を見る。

## MmmBatch (リマインダーの送信)

設計は [specs/mmmbatch.md](specs/mmmbatch.md)。土台 (ソリューション・exe・トレイ常駐・送信の処理・DB の保存先・送信の状況の画面・接続の設定画面)と、MmmTool 側の送信先の登録と選択は実装済み。DB の表は作成済み。実機で動かした確認は、まだ。

- MmmBatch の配布の形: 出力の形 (Lib・ライセンス全文・発行プロファイル)は MmmTool と同じにした ([build-and-distribution.md](build-and-distribution.md))。アイコンは MmmTool と別 (緑の角丸の四角・白い歯車・紫の再生マーク)
  - 版・説明書 `Distribution/README.txt`・MmmBatch の README は不要 (サーバー用の特別なアプリで、使う人が限られるため)

## メッセージ送信

設計は [specs/messages.md](specs/messages.md)・[specs/mmmbatch.md](specs/mmmbatch.md) の「メッセージの送信」・[specs/database.md](specs/database.md) の `MessageSendRequest`。画面 (MmmTool)・依頼の保存・MmmBatch の送信とログ・画面は実装済み。実機で動かした確認は、まだ。

- 送信済み・失敗を MmmTool の画面で見られるようにする (依頼の行の状態を読む。出す場所・見せ方は未定)
- 未決定: MmmBatch が長く止まっていたとき、古い依頼を起動後に送るか (今の仕様は、送信待ちの行を、古さにかかわらず送る)
- 未決定: 画面の文言 (仮)。本文欄の高さ (最小 120 と 240)を、行数から計算した値にする

## DB 保存 (SQL Server が最初)

設計は [specs/database.md](specs/database.md)。DB のコードの置き場所は [architecture.md](architecture.md) の「DB のコードの置き場所」。作業は小さな単位に分ける。土台 (表・行のクラス・ユーザー・保存先・接続の設定・DI・起動時のユーザー特定)と画面 (設定ページの保存先・初回の保存先の選択・宛先・削除の出し分け)は実装済み。

- DB のバックアップの計画: 今は一度も取っていない。復旧モデルは単純なので、全体のバックアップを定期的に取る形を、ユーザーと相談する
- 接続用のパスワード (検証用に簡単なもの)を、本番用に替えるか: `ALTER LOGIN [MmmTool] WITH PASSWORD = N'新しいもの'` で替え、アプリの設定画面で入れ直す
- 置き場所の基準 (DB の表か、この PC の設定か): 他の人と共有するなら DB。その人の見え方の選択 (表示列・表示する案件など)は、この PC の設定。別の PC から追いかけてほしいかは、ユーザーが決める要件 ([specs/database.md](specs/database.md) の「設計の判断」)
- 方向だけ決めている後回し: ユーザーごとの設定を DB に置くときは、縦持ち (`(UserId, SettingKey)` の主キー・キーは `Reminder.SnoozeIntervalMinutes` のような文字列・値は JSON)にし、`ISettingsStore` と同じ形で読み書きする

## 作業管理

設計と実装済みの範囲は [specs/work-items.md](specs/work-items.md)。保存の置き換え (ローカルは SQLite の `Data/MmmTool.db`、DB モードは SQL Server。表 5 つ)まで実装済み。JSON の保存は廃止した。

- コードマスタ ([specs/work-items.md](specs/work-items.md) の「コードマスタ」): 残りは、コードマスタの管理ツール。別ツールにする (選択肢の追加・外す・並べ替え。ローカルモードの扱いは未定)
- 後で決める: リーダー的な人の更新の権限 (SQL Server の実装のとき)・リマインダーを SQLite に移すか (実データがあり、移行が要る)
- 方向だけ決めている後回し: ガントチャート (別の画面。同じデータを読む)・Backlog との連携 (状態・優先度・進捗度を数値で対応づける)
- 未決定: 表の案件の右クリックに「非表示にする」を足すか ([specs/work-items.md](specs/work-items.md) の「表示する案件」)

## ビルドの警告

- NuGet の脆弱性の警告 `NU1902` (中)・`NU1903` (高): `System.Security.Cryptography.Pkcs` 9.0.18 に既知の脆弱性がある。SQL Server に接続する部品 (`Microsoft.Data.SqlClient`)が内部で使っているとみられ、`MmmTool.Data.SqlServer` を含むプロジェクトで出る
  - 直し方の案: `Directory.Packages.props` で、このパッケージの新しい版を指定する (パッケージの追加にあたるので、内容を説明して確認を取ってから行う)
  - 先に、変更前のコードでも出ていたか、新しい版が `Microsoft.Data.SqlClient` と合うかを調べる

## 調査中

- CLI補助の送信で、まれに Enter が効かない (貼り付けが `[Pasted text …]` のまま入力欄に残る)
  - 調査用の記録を SDK に入れてある (`TEMP-LOG` の印の行。master に入っている。`%TEMP%\MmmTool\terminal-submit.log`)。これまでの失敗は、どれも添付つきの送信
  - 失敗の記録が取れたら、Enter を出す条件を直し、記録を外す
  - 経緯と方針は [specs/cli-assist.md](specs/cli-assist.md) の「送信の Enter のタイミング」

## そのうち (方向だけ)

- サーバー側のビルド (GitHub Actions)で、タグを付けたら発行からリリースまで自動で行う。ユーザーが試したがっている。リポジトリは公開で、Actions は無料
  - 調べること: 手元と同じ配布物になるか、`dotnet publish` の時間、SDK の取得
  - 最初は、zip を実行結果の添付に置くだけの手動実行で試す

## ユーザーが用意するもの待ち

- README.md の画像: 画像はユーザーが用意する (リンクの中身などがまだ決まっておらず、撮れない)。届いたら `docs/images/` に下の名前で置き、「主な機能」の各機能の見出しの下に添える (CLI補助の `cli-assist.jpg` と同じ形)
  - リンクの編集画面 `links.png`・トレイの「リンク」メニュー `tray-links.png`・通知ダイアログ `notification.png`・リマインダーのメイン画面 `reminders.png`
- 冒頭 (ツカミの下)には、特定の機能に偏らない画像だけを置く。CLI補助はメイン機能ではなく、便利機能の詰め合わせの 1 つなので、CLI補助の画像を冒頭に置くと、CLI補助のアプリに見える
  - 候補はトレイのリンクメニューと通知ダイアログ (常駐していて邪魔にならないことが伝わる)。どれを冒頭に置くかは、画像が届いたときにユーザーと決める
