# 設定ページ

`Features/Settings/SettingsPage` ＋ `SettingsViewModel`。メイン画面のサイドバー下部に置く。

## 構成 (機能ごとの部品を並べる)
- 設定の項目は、各機能が `AddSettingsSection<TControl>()`(`Shell/ShellServiceCollectionExtensions`。サイドバーの `AddNavigationPage` と同じ形)で、設定の部品 (`UserControl`)を登録する。設定ページは、登録順に縦に並べるだけで、特定の機能を知らない。機能を足すときに触るのは、その機能のフォルダと `App` の 1 行だけ
- 部品は設定ページを開くときに DI から作る (Transient)。値の読み書きと画面の状態は、その機能の ViewModel・サービスが持つ (部品の見た目は設定ページと同じ縦並び)
- 各行は「名前の列 + 操作」の 2 列の Grid で、名前の列の幅は共通のリソース (`App.xaml` の `SettingNameColumnWidth`。240)にそろえる。名前の長さが違っても、スイッチ・入力欄の左端が縦にそろう (WinUI 3 には `SharedSizeGroup` が無いため、固定の幅にする。名前がこの幅を超えるときは、リソースの値を広げる)
- `SettingsViewModel` はページ全体のことだけ：タイトルと、設定ファイルを読めなかったとき (`ISettingsStore.LoadError`)の InfoBar。読めなかったときは、各部品が、自分のサービスの `IsReadOnly` で入力欄を無効にする (元のファイルを上書きで消さないため)

## 機能のオン・オフ
- ページの先頭に、機能の一覧を出す (見出しは付けない。リマインダーの項目と同じ、左に名前・右に操作の横並び)。オフにできる機能 (`AddFeature` で登録したもの。今は CLI補助・クリップボード転送・Backlog)を、登録順に `ToggleSwitch`(オン / オフ)で並べる。リンク・リマインダーはオフにできないので出さない。一覧は `FeatureService.Features` から作るだけで、特定の機能を知らない (`SettingsViewModel` ＋ `FeatureItem`)。設定の部品は、その機能がオン・オフされたときだけ並べ直す
- 切り替えはすぐ反映する (再起動は要らない)。サイドバー・トレイメニュー・設定ページの部品が、`FeatureService.Changed` で変わる。オフにした機能のページは捨て (`PageProvider.EvictDisabledPages`)、表示中なら先頭のページへ移る。オンにしたときは、その機能の起動時の準備を実行する
- 状態は設定ストアの `Feature.<キー>.Enabled`(bool)。保存が無い機能はオン
- CLI補助を、ターミナルが動いている間にオフにするときは、確認ダイアログを出す (「実行中のターミナルと、その中の作業が終了します」。`CliAssistDisableConfirmation`)。ターミナルが動いていなければ確認しない。取りやめると、スイッチは元に戻る
- 設定ファイルを読めなかったとき (`IsReadOnly`)は、スイッチを無効にする。保存できなかったとき (読み込めない・`DataFileException`)は InfoBar で知らせ、スイッチを元に戻す
- 仕組みと決めた理由は、下の「決定の理由」

## 項目
### リマインダーのスヌーズ間隔 (`Features/Reminders/ReminderSettingsControl` ＋ `ReminderSettingsViewModel`)
- `NumberBox`(スピンは Inline・小さい刻み 1 / 大きい刻み 10)と双方向で、変わったら即保存する
- 保存は `ReminderSettingsService`(Core)経由で、設定ストアの `Reminder.SnoozeIntervalMinutes` に委譲する
- 範囲は 5〜999 分 (定数と補正は `ReminderSettingsService` の `MinSnoozeInterval` / `MaxSnoozeInterval` / `ClampSnoozeInterval`)、既定は 15。範囲外・空は範囲内に補正して、画面にも反映する (`NumberBox` は空にすると NaN になる)
- 保存の失敗 (`DataFileException`)は、画面の InfoBar に出して続ける (`SaveSnoozeIntervalAsync`)。想定外の失敗は、握りつぶさず安全網 (ログ → ダイアログ → 終了)へ流す

### Backlog の API キー (`Features/Backlog/Settings/BacklogSettingsControl` ＋ `BacklogSettingsViewModel`)
- `PasswordBox` に API キーを入れる。入力欄からフォーカスが外れたときに保存する。保存先は設定ストアではなく、資格情報マネージャー ([backlog.md](backlog.md))
- 保管庫を読めない・書けない失敗 (`SecretStoreException`)は、画面の InfoBar に出して続ける。Backlog の機能がオフの間は、この項目も並べない

## 決定の理由

### 機能のオン・オフ
- 機能が増えても、使わない機能はサイドバー・トレイ・常駐処理から外したい。まず CLI補助だけをオフにできるようにし、機能が増えたときも同じ形で足せる土台にする。リンクとリマインダーはオフにしない (ユーザーの決定)
- **登録の単位を「機能」にする。** オフにできる機能は、`Add<機能>()` の中で `AddFeature(キー, 表示名)` を登録し、ページ・起動時の準備・トレイメニュー・設定の部品の登録に同じキーを渡す。Shell は機能の名前を知らず、キーで絞り込むだけ。ページを持たない機能 (トレイだけ・常駐だけ)も同じ形で扱える。キーは文字列の定数 (`CliAssistFeature.Key`)で、設定ファイルにも使うので、決めたら変えない
- **状態は設定ストアにキー単位で保存し、保存が無ければオン。** 機能を足しても、保存済みの設定の移行は要らない。リンク・リマインダーは `AddFeature` を登録しないので、一覧にも出ず、絞り込みの対象にもならない
- **切り替えはすぐ反映する** (ユーザーの決定。トレイ常駐 + 多重起動の禁止のアプリで、再起動を求めると操作が重い。PowerToys など常駐ツールのモジュールの切り替えと同じ)。`FeatureService.Changed` を、サイドバー (`MainViewModel.Refresh`)・ページのキャッシュ (`PageProvider.EvictDisabledPages`)・設定ページの部品が受ける。トレイメニューは開くたびに項目を作るので、`FeatureTrayMenuSource` が空を返すだけで反映される (SDK の変更は不要)
- **オフにしたページは捨てる。** ルートのプロバイダーから作った `IDisposable` の Transient は、Host の破棄まで残る (オン・オフのたびに増え、ページ・WebView2 も残る)ので、`PageProvider` がページごとのスコープ (`IServiceScopeFactory`)から作り、オフにしたときにスコープを破棄する。これでセッション (シェルとその中の CLI)が終わる。画面の部品に触れる後始末は `IReleasablePage.Release`(CLI補助は、ターミナルのコントロールからセッションを外す)に書き、スコープの破棄の前に呼ぶ (`IDisposable` をページに付けると、アプリの終了時にも DI が呼び、そのとき画面の部品に触れてしまう)。残りのスコープは、`PageProvider` の `Dispose`(Host の破棄)で破棄する。オンに戻したときは、ページ・セッションを新しく作る
- **確認の取り出し**: `PageProvider` は `FeatureService` に、`FeatureService` は確認 (`IFeatureDisableConfirmation`)に依存するので、CLI補助の確認は、ページを引くとき (確認の実行時)に `IServiceProvider` から `PageProvider` を取り出して、循環を避ける
- **オフにする前の確認** (ユーザーの決定): CLI補助は、ターミナルが動いているときだけ、確認ダイアログを出す (作業中のセッションが、誤操作で消えるのを防ぐ)。確認は機能ごとに `IFeatureDisableConfirmation` で登録する (確認が要らない機能は登録しない)
- **起動時の準備**: `IStartupTask` は `FeatureService` が持つ登録の一覧から、オンの機能の分だけ登録順に実行する (共通の設定ファイルの先読みが先頭なので、オン・オフの判定は読み込み済みの設定で行われる)。起動時にオフの機能は実行せず、オンにしたときに実行する
- **UI**: 設定ページの先頭に、オフにできる機能だけを `ToggleSwitch` で並べる。オフにできないものを無効のスイッチで並べると、押せないものが増えて紛らわしいので出さない
