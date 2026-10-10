# 設定ページ

`Features/Settings/SettingsPage` ＋ `SettingsViewModel`。メイン画面のサイドバー下部に置く。

## 構成 (機能ごとの部品を並べる)
- 設定の項目は、各機能が `AddSettingsSection<TControl>()`(`Shell/ShellServiceCollectionExtensions`。サイドバーの `AddNavigationPage` と同じ形)で、設定の部品 (`UserControl`)を登録する。設定ページは、並び順の値の順に縦に並べるだけで、特定の機能を知らない。機能を足すときに触るのは、その機能のフォルダと `App` の 1 行だけ
- 並び順は、部品の並び順の値 (小さいほど先)。各機能の部品は、機能の並び (利用者が決めた順。上の「機能のオン・オフと並び順」。登録の `order` は、保存が無いときの順)で並ぶ。機能に属さない部品は `Shell/SettingsSectionOrder` の値で、すべて機能より前に並ぶ。ページは「全般」→「保存先」→「アカウント」(DB のログイン済みのときだけ)→「機能」の一覧 → 各機能 (機能の並びの順。リマインダー → Backlog など)の順に並べるだけで、どの部品も特別扱いしない。「保存先」はアプリ全体の基本設定なので、各機能の設定より上にする
- 部品は設定ページを開くときに DI から作る (Transient)。値の読み書きと画面の状態は、その機能の ViewModel・サービスが持つ (部品の見た目は設定ページと同じ縦並び)
- 見た目は、Windows 11 の設定アプリと同じ「見出し + カード」。区切りの見出しは各部品が先頭に持ち (`App.xaml` の `SettingsSectionHeaderStyle`)、項目は `SettingsCard`(`CommunityToolkit.WinUI.Controls.SettingsControls`)の行にする。左に名前 (`Header`)と説明 (`Description`)、右に操作を置く。ページは特定の機能を知らないまま、部品を並べるだけ
- 設定ページ全体は `ScrollViewer` に入れ、項目が画面に収まらないときも、下まで届く
- 項目の縦の間隔は、ページ・各部品とも `StackPanel Spacing="4"` でそろえる (カードの間隔)。各部品の先頭にあるエラーの `InfoBar` は、閉じている間も要素が残って `Spacing` を取ってしまうので、`Visibility` を `ErrorState.IsOpen` にバインドして、閉じている間は場所を取らないようにしている。新しい設定の部品を足すときも、同じにする
- 保存の方式は 2 つ: 値が 1 つの項目 (スイッチ・数・API キー)は、変えた時点で保存する (保存ボタンは無い)。保存先と接続は、カードから開く別のウィンドウで編集し、そこの「保存」で保存する。一括の「保存」は無い (理由は下の「決定の理由」)
- 新しい設定の部品は `SettingsControls` のパッケージが要る (参照の仕方は、`Segmented` と同じ。[build-and-distribution.md](../build-and-distribution.md))
- `SettingsViewModel` はページ全体のことだけ：タイトルと、設定ファイルを読めなかったとき (`ISettingsStore.LoadError`)の InfoBar。機能のオン・オフの一覧は、部品 (`Features/Settings/FeatureList/FeatureListControl` ＋ `FeatureListViewModel` ＋ `FeatureItem`)が持つ。読めなかったときは、各部品が、自分のサービスの `IsReadOnly` で入力欄を無効にする (元のファイルを上書きで消さないため)

## 機能のオン・オフと並び順
- 「全般」の下に、機能の一覧を「機能」の見出し (と説明文 1 行)の下に出す。各行は `SettingsCard`(左に取っ手 ⠿ と機能名、右に操作)。オフにできる機能 (`AddFeature` で登録したもの。今は CLI補助・クリップボード転送・Backlog・作業リスト)には `ToggleSwitch`(オン / オフ)を付け、リンク・リマインダー (`AddAlwaysOnFeature`。`FeatureInfo.CanDisable = false`。`IsEnabled` は常に true)は、スイッチなしで並べる。一覧は `FeatureService.Features` から作るだけで、特定の機能を知らない (`FeatureListViewModel` ＋ `FeatureItem`)。設定の部品は、その機能がオン・オフされたときだけ並べ直す
- 行はドラッグして並べ替える (`ListView` の `CanReorderItems`。保存ボタンは無く、一覧の変更 (`CollectionChanged`)を見て、操作が止まってから 300ms 後に 1 回だけ保存し (`Debouncer`。`DragItemsCompleted` は届かなかったため使わない)、すぐ反映する。失敗したときは InfoBar を出して、実際の並びへ戻す)。取っ手は「よく使う文の編集」と同じ。一覧の下の「既定の順に戻す」で、保存した並びを消して登録の順に戻す
- 並びは、利用者が決めた順 (保存が無ければ、登録の並び順の値の順: CLI補助 → 作業リスト → リマインダー → リンク → クリップボード転送 → Backlog)。反映先は、サイドバーと、設定ページの各機能の部品。「全般」「保存先」「アカウント」「機能」の一覧は、先頭に固定 (動かせない)。トレイは連動せず、登録の order の順 (リマインダー → リンク → 終了)のまま
- 保存は、設定ストアの `Feature.Order`(機能のキーの配列。SDK の `FeatureService`)。載っていない機能 (あとから足したもの)は、後ろに登録の順で付く。並びの値は `FeatureService.OrderOf(キー, 登録の値)` が返し、サイドバー (`MainViewModel`)と設定ページ (`SettingsPage`)が使う (並べる処理を 1 か所に置く)。並びが変わると `OrderChanged` が出る (`Changed` はオン・オフ用)
- 設定ページは、すでに並んでいる部品を作り直さず、順序の違うものだけを動かす (ドラッグの直後に「機能」の一覧を作り直さない)。サイドバーも、項目を `Move` する
- 文言 (説明文「使う機能とサイドバーの並び順」・ボタン「既定の順に戻す」・エラーの文)は案。決めてから直す。キーボードでの並べ替えは、実機で確かめてから書く (WinUI の標準で動くかを未確認)
- 切り替えはすぐ反映する (再起動は要らない)。サイドバー・トレイメニュー・設定ページの部品が、`FeatureService.Changed` で変わる。オフにした機能のページは捨て (`PageProvider.EvictDisabledPages`)、表示中なら先頭のページへ移る。オンにしたときは、その機能の起動時の準備を実行する
- 状態は設定ストアの `Feature.<キー>.Enabled`(bool)。保存が無い機能は、登録の既定値 (`AddFeature` の `defaultEnabled`。省略するとオン)。今は、CLI補助・クリップボード転送・Backlog・作業リストが既定でオフ
- CLI補助を、ターミナルが動いている間にオフにするときは、確認ダイアログを出す (「実行中のターミナルと、その中の作業が終了します」。`CliAssistDisableConfirmation`)。ターミナルが動いていなければ確認しない。取りやめると、スイッチは元に戻る
- 設定ファイルを読めなかったとき (`IsReadOnly`)は、スイッチを無効にする。保存できなかったとき (読み込めない・`DataFileException`)は、一覧の見出しの下の InfoBar で知らせ、スイッチを元に戻す
- 仕組みと決めた理由は、下の「決定の理由」

## 項目
### 起動時にメイン画面を開く (`src/App/MmmTool/Features/Settings/General/MainWindowSettingsControl` ＋ `MainWindowSettingsViewModel`)
- ホスト側の部品で、`AddSettingsSections()` が `AddSettingsSection` で登録する (どの機能にも属さず、オフにならない)。設定の部品のうち最初に登録するので、ページの一番上に並ぶ (Windows の設定アプリも、全般が先頭)。見出しは「全般」。設定の読み書き `MainWindowSettingsService` は、メインウィンドウと同じ `Shell/` にある
- `ToggleSwitch`(オン / オフ。機能の一覧と同じ)で、変えた時点で保存する。保存は `MainWindowSettingsService` 経由で、設定ストアの `Shell.OpenMainWindowOnStartup`(bool。無ければオフ)。説明の文言は付けない
- 設定ファイルを読めなかったとき (`IsReadOnly`)は、スイッチを無効にする。保存の失敗 (`DataFileException`)は、画面の InfoBar に出す
- 反映は次の起動から。起動の動きは [tray-and-main.md](tray-and-main.md)

### 音声機器を起こしておく (`MainWindowSettingsControl` ＋ `MainWindowSettingsViewModel`。「全般」の 2 つ目の項目)
- 光デジタル出力など、無音が続くと信号を止める機器で、読み上げの頭が切れるときに、オンにする。オンの間、無音の出力 (SDK の `IAudioKeepAlive`。仕組みは SDK の `docs/audio.md`)を流し続ける
- `ToggleSwitch`(オン / オフ)で、変えた時点で保存し、すぐに始める・止める。説明文は「無音を流し続けて、音声機器が休止するのを防ぎます。通知の読み上げの頭が切れるときに、オンにします。」。既定はオフ
- 保存と、始める・止めるは `Shell/AudioKeepAliveSettingsService`。設定ストアの `Shell.AudioKeepAlive`(bool。無ければオフ。PC ごとの音声機器の事情なので、DB モードでも設定ストアに置く)。起動時は `AudioKeepAliveStartup` が、オンなら始める
- 設定ファイルを読めなかったとき・保存の失敗の扱いは、「起動時にメイン画面を開く」と同じ

### リマインダーのスヌーズ間隔 (`src/Plugins/MmmTool.Reminders/ReminderSettingsControl` ＋ `ReminderSettingsViewModel`)
- `NumberBox`(スピンは Inline・小さい刻み 1 / 大きい刻み 10)と双方向で、変わったら即保存する
- 保存は `ReminderSettingsService`(Core)経由で、設定ストアの `Reminder.SnoozeIntervalMinutes` に委譲する
- 範囲は 5〜999 分 (定数と補正は `ReminderSettingsService` の `MinSnoozeInterval` / `MaxSnoozeInterval` / `ClampSnoozeInterval`)、既定は 15。範囲外・空は範囲内に補正して、画面にも反映する (`NumberBox` は空にすると NaN になる)
- 保存の失敗 (`DataFileException`)は、画面の InfoBar に出して続ける (`SaveSnoozeIntervalAsync`)。想定外の失敗は、握りつぶさず安全網 (ログ → ダイアログ → 終了)へ流す

### リマインダーの読み上げの声・速さ (同じ `ReminderSettingsControl` の 2 行目)
- 声と速さはアプリでは持たず、Windows の音声の設定 (時刻と言語 > 音声)に従う。そこへ移るリンク「Windows の音声設定を開く」(`HyperlinkButton` ＋ 外部リンクのアイコン)を置く。`ms-settings:speech` を `IPathOpener` で開く (`OpenSpeechSettingsAsync`)
- 強調しないリンク型にしたのは、アプリの外へ出るだけの操作で、この画面の主な操作ではないため
- 開けなかったとき (`PathOpenException`)は、画面の InfoBar に出す

### Backlog の API キー (`src/Plugins/MmmTool.Backlog/Settings/BacklogSettingsControl` ＋ `BacklogSettingsViewModel`)
- `PasswordBox` に API キーを入れる。入力欄からフォーカスが外れたときに保存する。保存先は設定ストアではなく、資格情報マネージャー ([backlog.md](backlog.md))
- 保管庫を読めない・書けない失敗 (`SecretStoreException`)は、画面の InfoBar に出して続ける。Backlog の機能がオフの間は、この項目も並べない

### アカウント (`src/App/MmmTool/Features/Users/Settings/AccountSettingsControl` ＋ `AccountSettingsViewModel`)
- ホスト側の部品で、`AddUserSignIn()` が `AddSettingsSection` で登録する (並びは「保存先」の次)。DB に保存していて、ログイン済みのときだけ出す (隠している間は、ページの `Spacing` の場所も取らない)。見出しは「アカウント」。内容と動きは [database.md](database.md) の「アカウントの変更とログアウト」
- カード: 「表示名」「ログイン名」(説明に今の値)「パスワード」は、押すと編集のウィンドウが開く。「ログアウト」は、右に強調しないボタン (押すと確認が出る)

### 保存先と DB への接続 (`src/App/MmmTool/Features/Database/Settings/DatabaseSettingsControl` ＋ `DatabaseSettingsViewModel`)
- ホスト側の部品で、`AddDatabaseScreens()` が `AddSettingsSection` で登録する (どの機能にも属さず、オフにならない)。設計は [database.md](database.md)
- 設定ページには、「保存先」の見出しの下に、カード 1 枚だけを出す (「保存先」。説明に、今の保存先を「ローカル」または「サーバー (サーバー名)」で見せる)。カード全体が押せて (右に ›)、押すと編集のウィンドウが開く。閉じたら、説明を読み直す
- 編集のウィンドウ (`Features/Database/Edit/DatabaseEditWindow`。開く口は `IDatabaseDialogService`): モーダルのウィンドウで、保存先 (ローカル / DB)を `RadioButton` で選ぶ。DB のときだけ、接続の入力欄 (接続先のサーバー・データベース名・ユーザー名・パスワード)と「接続を確認」を出す。DB のときは、つながると確認できるまで、「保存」を押せない (ログインの方式と証明書は、画面の項目にしない。理由と動きは [database.md](database.md))。入力欄は、初回の保存先の選択の画面と共有する部品 (`DatabaseConnectionForm`)
- 保存は「保存」ボタンを押したときだけ (入力の途中を保存しない)。保存先は起動時に決まるので、保存先の種類が変わった・DB のまま接続が変わったときは、「反映するには、アプリの再起動が必要です。今すぐ終了しますか？」と確認する (「今すぐ終了」でアプリを終了。「あとで」は保存だけ)。終了は `AppExitService` が、トレイの「終了」と同じ処理で行う。保存できたら、ウィンドウを閉じる。「閉じる」・× は、保存しない
- 失敗は、ウィンドウの先頭の InfoBar で知らせる。成功 (「接続できました。」)も InfoBar。設定ファイルを読めなかったとき (`IsReadOnly`)は、カードと入力・「保存」を無効にする

## 決定の理由

### 機能のオン・オフ
- **並び替えは、「機能」の一覧に持たせる (専用の欄は作らない。2026-10-11、ユーザーと決定)。** 機能は 6 つで、同じ名前の一覧を 2 つ並べると画面が長くなるだけ。1 つの一覧で、オン・オフと位置を変えられるほうが迷わない (VS Code のアクティビティバー・Chrome のツールバーのカスタマイズも同じ形)。見出しを「表示・非表示」にしないのは、オフにすると起動時の読み込みも止まり、動きが伝わらないため。機能が増えて並びだけ別に整えたくなったら、一覧から分ければ足りる (今の形が作り直しにならない)。トレイを連動させないのは、ユーザーの決定
- 機能が増えても、使わない機能はサイドバー・トレイ・常駐処理から外したい。まず CLI補助だけをオフにできるようにし、機能が増えたときも同じ形で足せる土台にする。リンクとリマインダーはオフにしない (ユーザーの決定)
- **登録の単位を「機能」にする。** オフにできる機能は、`<機能>Plugin.Register` の中で `AddFeature(キー, 表示名)` を登録し、ページ・起動時の準備・トレイメニュー・設定の部品の登録に同じキーを渡す。Shell は機能の名前を知らず、キーで絞り込むだけ。ページを持たない機能 (トレイだけ・常駐だけ)も同じ形で扱える。キーは文字列の定数 (`CliAssistFeature.Key`)で、設定ファイルにも使うので、決めたら変えない
- **オン・オフの管理 (`FeatureService`・`FeatureChangeResult`)は SDK (`MmmSdk.Core.Components.Features`)に置く。** UI にも特定の機能にも依存せず、設定ストア・機能の登録・起動時の準備だけで動くので、ほかのアプリも使える (最終形: 共通の部品は SDK に集める)。理由は SDK の `docs/architecture.md` の「機能 (プラグイン)がホストへ入る口」。ホストは `AddFeatureService()` で登録するだけで、サイドバー・ページのキャッシュ・設定ページへの反映と、オフにする前の確認の画面は、ホストが持つ
- **状態は設定ストアにキー単位で保存し、保存が無ければオン。** 機能を足しても、保存済みの設定の移行は要らない。リンク・リマインダーは `AddAlwaysOnFeature` で登録する (並び順のためだけ。常にオンで、絞り込みの対象にならない)
- **切り替えはすぐ反映する** (ユーザーの決定。トレイ常駐 + 多重起動の禁止のアプリで、再起動を求めると操作が重い。PowerToys など常駐ツールのモジュールの切り替えと同じ)。`FeatureService.Changed` を、サイドバー (`MainViewModel.Refresh`)・ページのキャッシュ (`PageProvider.EvictDisabledPages`)・設定ページの部品が受ける。トレイメニューは開くたびに項目を作るので、`RegisteredTrayMenuSource` が空を返すだけで反映される (SDK の変更は不要)
- **オフにしたページは捨てる。** ルートのプロバイダーから作った `IDisposable` の Transient は、Host の破棄まで残る (オン・オフのたびに増え、ページ・WebView2 も残る)ので、`PageProvider` がページごとのスコープ (`IServiceScopeFactory`)から作り、オフにしたときにスコープを破棄する。これでセッション (シェルとその中の CLI)が終わる。画面の部品に触れる後始末は `IReleasablePage.Release`(CLI補助は、ターミナルのコントロールからセッションを外す)に書き、スコープの破棄の前に呼ぶ (`IDisposable` をページに付けると、アプリの終了時にも DI が呼び、そのとき画面の部品に触れてしまう)。残りのスコープは、`PageProvider` の `Dispose`(Host の破棄)で破棄する。オンに戻したときは、ページ・セッションを新しく作る
- **確認の取り出し**: `PageProvider` は `FeatureService` に、`FeatureService` は確認 (`IFeatureDisableConfirmation`)に依存するので、CLI補助の確認は、ページを引くとき (確認の実行時)に `IServiceProvider` から `PageProvider` を取り出して、循環を避ける
- **オフにする前の確認** (ユーザーの決定): CLI補助は、ターミナルが動いているときだけ、確認ダイアログを出す (作業中のセッションが、誤操作で消えるのを防ぐ)。確認は機能ごとに `IFeatureDisableConfirmation` で登録する (確認が要らない機能は登録しない)
- **起動時の準備**: `IStartupTask` は `FeatureService` が持つ登録の一覧から、オンの機能の分だけ登録順に実行する (共通の設定ファイルの先読みが先頭なので、オン・オフの判定は読み込み済みの設定で行われる)。起動時にオフの機能は実行せず、オンにしたときに実行する
- **UI**: 設定ページの先頭に、機能を一覧で並べ、オフにできる機能だけに `ToggleSwitch` を付ける。オフにできないものを無効のスイッチで並べると、押せないものが増えて紛らわしいので、行は出してもスイッチは付けない (並び順を決める行として並べるため)

### 設定ページの見た目と、保存先の編集
- **「全般」と「機能」の一覧も、登録する部品にする。** 一覧をページに直接書くと、並ぶ部品は必ずその下に出てしまい、「全般」を一番上にできない。ページに「全般」を特別扱いで置くと、ページが特定の部品を知ることになるので、一覧も部品にして、並びを並び順の値だけで決める (最終形: ページは並べるだけ。旧: 登録順で、保存先が各機能の下になっていた。仕組みの都合で決まった並びだったので、値にした)
- **見出しとカードにする** (ユーザーの決定。Windows 11 の設定アプリ・PowerToys と同じ現行の形): 名前と説明を左、操作を右に置く行 (`SettingsCard`)を、見出しごとにまとめる。最終形: 機能が増えても、各機能の部品が自分の見出しとカードを持つだけで、ページは特定の機能を知らずに並べられる
- **保存の方式を 2 つに分ける**: 値が 1 つの項目 (スイッチ・数・API キー)は、変えた時点で保存する。保存先と接続だけは、確認 (「接続を確認」)を経て保存する性質なので、カードから開く別のウィンドウに、その流れを閉じ込める。設定ページの中に即保存の項目と「保存」のボタンが並ぶと、「保存」が全体の保存に見えて紛らわしいため (ユーザーの指摘)。最終形: 設定ページには「保存」が無く、保存が要る設定は、それぞれの編集画面が持つ
- **編集は、モーダルのウィンドウにする**: `ContentDialog` にすると、その上で出る確認 (再起動)と同時に開けず、例外になる。`DialogService` は、いちばん手前のモーダルを確認の親にするので、ウィンドウなら確認が重なって出る。保存先の選択は、初回の選択の画面と同じ `RadioButton` にそろえた
- **「今すぐ終了」は、ウィンドウを閉じてから頼む**: 終了の処理 (各機能の後始末・Host の停止)が、開いたままのウィンドウと重ならないようにするため

- **「音声機器を起こしておく」は、既定をオフにする。** 光デジタルなど一部の機器でだけ要る機能で、無音を流し続けるのは、全員の PC にとって余計な負担になるため
- **リマインダーの設定ではなく、「全般」に置く。** 無音の出力は、読み上げ・通知音など、アプリ全体の音の頭が切れるのを防ぐ、音声機器の設定で、リマインダーに属さないため (SDK の部品も、`Speech` とは別の `Audio`)
- **設定ストア (この PC のファイル)に置き、DB に置かない。** 音声機器は PC ごとの事情で、ユーザーごとの設定ではないため (「起動時にメイン画面を開く」と同じ)
