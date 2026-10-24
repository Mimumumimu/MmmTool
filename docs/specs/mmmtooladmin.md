# MmmToolAdmin

サーバーモード (DB モード)の管理の操作を行うアプリの仕様。

## 位置づけ

- MmmTool・MmmBatch とは別のアプリで、別の exe・別のソリューション `MmmToolAdmin.slnx` にする
  - リポジトリは同じ。機能 (`src/Plugins/`)・DB (`src/Data/`)・SDK (`external/MmmSdk`)を共有する。
- 管理の操作を、MmmBatch の exe に載せない ([mmmbatch.md](mmmbatch.md))
  - 理由: バッチは無人で動き続け、必要最小限の権限だけを持たせたいため。権限が強い機能を、起動しっぱなしの exe に載せない。
- 画面は WinUI の exe 1 つで、左にサイドバー、右に機能ごとのページを出す 1 枚のウィンドウ
  - サイドバーの本体は、MmmTool・MmmBatch と同じ SDK の `NavigationHost`
  - 常駐しない。ウィンドウを閉じると終了し、トレイへの退避は持たない。
- 使うのは、アプリの持ち主 1 人。配布しない。
  - アプリ自体のログインを持たない。入口は、DB のログインだけ。
  - ユーザーの特定 (`CurrentUser`)をしない。表の更新者の列には、0 (特定のユーザーではない)が入る。登録用コードを書く `AppUserWebAuth` の `UpdatedByUserId` は、Web 版の `issue-code` と同じく 0。

## 全体の形

| 場所 | 中身 |
| --- | --- |
| `src/App/MmmToolAdmin` | ホスト。Host の構築・起動と終了の順序・メインウィンドウ (サイドバーを載せる)を持つ。個々の機能の名前を持たない |
| `src/Data/MmmToolAdmin.Data.SqlServer` | DB の登録の入口 `AddMmmToolAdminSqlServer`。接続の設定・ユーザー・登録用コードの保存先を登録する |
| `src/Plugins/MmmToolAdmin.Users` | 機能「ユーザー」。ページ `UsersPage` と ViewModel `UsersViewModel` を持つ |
| `src/Data/MmmTool.Data.SqlServer.WinUI` | MmmTool・MmmBatch と共有する。接続の設定画面・その開き口・DB を読む画面の基底 `DatabaseViewModelBase`・読み込みの失敗の帯 `ConnectionErrorBar` |

- 機能は、`src/Plugins/MmmToolAdmin.<機能>` のプラグインと、`App.ConfigureServices` の `AddFeaturePlugin<<機能>Plugin>(order: n)` の 1 行で足す
- 機能は、MmmToolAdmin の exe を参照しない。機能のプラグインどうしも参照しない。

## DB の接続

- ログインは、MmmTool と同じ `MmmTool`。管理ツール専用のログインは作らない。
- パスワードの保存名は、MmmTool と同じ `MmmTool.Database`。MmmTool が資格情報マネージャーに保存したパスワードを、同じ Windows ユーザーの管理ツールが、そのまま読む。
  - 保存先は、今の Windows ユーザーの、この PC の中だけ ([external/MmmSdk/docs/storage.md](../../external/MmmSdk/docs/storage.md))
  - 管理ツールのパスワードの画面で、空のまま保存すると、すでにあるパスワードを使う ([database.md](database.md) の接続の設定画面)
- サーバー名・データベース名・ユーザー名は、管理ツールの `Data/AppSettings.json` の `Database.*` に持つ。キーは [storage.md](storage.md) にある。MmmTool の設定ファイルは読まない。
  - 最初の起動で、接続の設定が保存されていないとき (`DatabaseSettingsService.IsModeSaved` が false)は、接続の設定画面を自動で開く。サーバー名を入れて保存するまで、毎回開く。
  - 画面が閉じたら、ページが自分で読み直す。きっかけは `IConnectionSettingsDialogService.ConnectionSettingsClosed` の通知。
  - つながらないときは、ページの上に帯を出し、「接続の設定」から画面を開く。起動は止めない。
- 接続の設定画面でパスワードを入れ直すと、MmmTool が使う `MmmTool.Database` も変わる。MmmTool も、次に開くときから、新しいパスワードで動く。

## ユーザー

- ユーザーの一覧を、番号の順に出す。削除済みと使える期間の外の人も含み、「状態」の列で、使える・削除済み・期間外のどれかを見分ける。
- 一覧で選んだ人の登録用コードを、「登録用コードを発行」で出す
  - 使える状態のユーザーを選んだときだけ押せる
  - 押すと、先に確認を出す。見出しは「登録用コードの発行」、本文は「○○ さんの登録用コードを発行します。前に出したコードは使えなくなります。」、ボタンは「発行」と「キャンセル」。既定のボタンは置かず、Enter では実行されない。
  - 発行の処理は、Web 版の `issue-code` と同じ `EnrollmentCodeService.IssueAsync` で、前に出したコードは使えなくなる ([web.md](web.md) の「ログイン」)
  - コードは、持ち主の名前・期限つきで、一覧の上に出す。コードは選んでコピーできる。
- ユーザーの追加・変更・削除・パスワードの初期化は、持たない。

## 機能の利用

ユーザーごとに、機能を使えるかを、管理者が決める。サーバーモードだけの操作。

- 画面は、「ユーザー」のページで、一覧の右に出る。人を選ぶと、その人の機能の一覧が出て、機能ごとに状態を選ぶと、すぐ保存する。
  - 人 × 機能の表にしない。人が増えると、表が成り立たないため。
  - 機能の一覧は、`MmmTool.Users.Core` の `ManagedFeatures` (機能番号の台帳 `CodeFeatures` の番号と、名前と、選べる状態)から作る。管理ツールのホストに、機能の名前を書かない。機能を足すときは、台帳に 1 行足す。
  - 対象は、DB を使う機能の、ノート・作業管理・リマインダー・メッセージ。DB を使わない機能は、対象にしない。
  - 選べるのは「有効」と「無効」。「読み取り専用」は、強制を作るまで選ばせない。選んでも有効と同じ動きになるため。強制は [todo.md](../todo.md) にある。
- 保存先は、[database.md](database.md) の `AppUserAdminSetting` の区分 1。行が無い人は、有効
  - 値は 0 = 無効・1 = 有効・2 = 読み取り専用。3 つの値を、最初から保存できる。
  - 更新者の列には、0 が入る。管理ツールは、ユーザーを特定しないため。
- 無効にした機能は、その人にとって「無い」ものにする。
  - PC 版 (MmmTool)は、サイドバーと、設定の「機能」の一覧から外す。管理者の「無効」は、ユーザー自身のサイドバーの切り替えより強い。
    - 機能の利用は、ユーザーを特定したときに読み込む。読み込むのは `FeatureAccessStartup`。覚えているログインで特定できたときは、サイドバーを作る前に読み込む。ログインの画面で特定したときは、特定のあとに読み込んで、サイドバーへ知らせる。管理者の変更は、次の起動から反映される。
    - 読み込めなかったときは、ログに残して、すべて有効のまま動く。
    - 作業管理は、設定でオンにしている人にだけ出る。オフでも、無効は無効のまま。
  - Web 版は、ホーム画面と設定の画面から外し、サーバーも、その機能の API を断る。詳細は [web.md](web.md) の「機能の利用」にある。反映は、次の操作から。
  - DB の権限は、変えない。PC 版は、画面から隠して止める。アプリを改造すれば、書き込める。
- メッセージとリマインダーは、無効の人に、送れず、届かない。
  - その人が無効の間は、宛先の一覧に出さない。依頼を書く処理も、宛先が無効なら断る。詳細は [messages.md](messages.md) と [reminders.md](reminders.md) にある。
  - すでに書かれたメッセージの通知は、MmmBatch が、送る直前に確かめて、無効なら送らず、`Skipped` にする。有効に戻っても、古い通知は出さない。詳細は [mmmbatch.md](mmmbatch.md) にある。
  - リマインダーの送信は、送信先の持ち主が無効の間、その送信先へ送らない。有効に戻った日は、その日の過ぎた時刻の分が、まだ送っていなければ送られる。前の日までの分は送らない。
  - メッセージ本体は消さない。有効に戻れば、会話に残っていて、見える。
  - グループの会話の扱いは、グループを作るときに決める。作る前でも、メッセージの保存は、グループでは、無効の人にだけ通知の状態を書かない形にしてある。

## 選択肢

- コードマスタ (表 `CodeMaster`で、全員で共通)の名前を、サーバーモードで編集する ([work-items.md](work-items.md) の「コードマスタ」)
  - 編集できるのは名前だけ。数値・表示順・選択肢の追加と外す操作は、画面から行わない。
  - 保存先は `SqlServerCodeMasterRepository` で、名前の列だけを書き、更新日時を DB の時計で進める。更新者は 0
  - 保存は、複数の名前を 1 つのトランザクションで行う
- 並べる部品 `CodeMasterSettingsControl` と編集ウィンドウは、MmmTool の設定ページの「選択肢」と同じものを使う。詳細は [settings.md](settings.md) の「選択肢」。ページの見出しが「選択肢」なので、部品の見出しは出さない。
- 編集できる区分は、プラグイン `MmmToolAdmin.CodeMaster` に 1 行ずつ足す。作業管理の「進捗度の名前」は、`WorkCodes.ProgressNameEditor` を使い、MmmTool の設定ページと同じ定義にする。
- MmmTool は、DB モードでは、名前を編集しない。保存先は、編集できない `UnavailableCodeMasterRepository` のままにする。DB モードの名前は、管理ツールだけが直す。
- 管理ツールの `App.xaml` に、共有の部品が使う見出しのスタイル `SettingsSectionHeaderStyle` と、入力欄の標準のスタイルを持つ。MmmTool の `App.xaml` と同じ内容。
