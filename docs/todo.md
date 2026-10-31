# 未実装・残りの作業

これからやることだけを書く。実装したら消し、説明は該当の `docs/` に移す。実装済みの機能は `docs/specs/` を見る。

## 作業管理

- 設計は [specs/work-items.md](specs/work-items.md) にある
- コードマスタの選択肢の追加・外す・並べ替えを作る ([specs/work-items.md](specs/work-items.md) の「コードマスタ」)
  - ローカルモードは、MmmTool の設定ページの「選択肢」で行う。
  - サーバーモードは、管理ツール MmmToolAdmin の「共通設定」の「選択肢」で行う。MmmTool は、サーバーモードでは、選択肢を編集しない。管理ツールの仕様は [specs/mmmtooladmin.md](specs/mmmtooladmin.md) を見る。
  - 名前の編集は、実装済み。
- 後で決める: リーダー的な人の更新の権限 (SQL Server の実装のとき)・リマインダーを SQLite に移すか (実データがあり、移行が要る)
- 方向だけ決めている後回し: Backlog との連携 (状態・優先度・進捗度を数値で対応づける)

## 管理ツール (MmmToolAdmin)

- 管理ツールの `App.xaml` に、MmmTool の `App.xaml` と同じスタイルを書き写している (`SettingsSectionHeaderStyle`・`AppTextBoxStyle`)
  - 方向: SDK の共有のリソースに移し、MmmTool と管理ツールの両方が読む。SDK の変更になるので、作る前に、ユーザーと決める。
- ユーザーごとの機能の「読み取り専用」(有効・無効・読み取り専用の 3 つ目の状態)
  - 保存する値は、最初から 3 つ (無効・有効・読み取り専用)で持つ。管理ツールでは、強制を作るまで選ばせない。
  - 作る内容: 機能ごとに、PC 版の画面の入力・保存・削除の操作を止める。Web 版は、機能ごとの API の入口で、読む要求 (GET)以外を断る。
  - 画面ごとの調整が要るので、有効・無効を作ったあとに、機能ごとに進める。作れたら、`ManagedFeatures` の選べる状態に読み取り専用を足す。
- 機能の利用で、リマインダーだけを無効にされた人は、設定の「受け取り先」と「送信先」の節が出ない
  - 2 つの節が、リマインダーの機能に属して登録されているため。メッセージを使う人が、受け取り先を指定できなくなる。
  - 方向: 「受け取り先」の節を、メッセージの機能に属する形に移す。節の部品が `MmmTool.Reminders` にあるので、置き場から決める。
- 機能の利用で、無効にされた機能の画面の道を、Web 版でアドレスに直接打つと、画面だけは開く (API が断るので、中身は出ない)
  - 方向: 画面側のルーターで、使えない機能の道を「見つからない」にする。
- 機能の利用で、無効にされたリマインダーの、PC 版の起動時の監視 (`ReminderStartup`)は、動き続ける
  - 無効の人は、リマインダーを何も読めないので、通知は出ない。監視そのものを止めるかは、決める。

## リマインダー

- 入力画面を、横長の 2 列にする。
  - 今は、すべての欄が 1 列に並び、幅 480 に対して高さが約 820 (幅の 1.7 倍)の縦長になっている。
  - 方向: 左の列に件名・日付・時刻、右の列に備考・リンク・読み上げ・宛先・送信先を置き、黄金比に近い横長にする。画面全体の作りを変えるので、作る前に案をユーザーと決める。

## メッセージ

- グループに後から入った人が、入る前のメッセージを読めるかを決める。読める範囲を絞るときは、参加者の行に「ここから読める」メッセージの番号の列を足す。決まりは [specs/messages.md](specs/messages.md) の「グループ」にある。
- 差分の取得で、長く使っていない端末の基準が古すぎるときは、最初から読み直す形にするかを決める。

## ノート

- 仕様は [specs/notes.md](specs/notes.md) にあり、表は [specs/database.md](specs/database.md) の `Note` にある
- そのうち (方向だけ): 実施日の iPhone への通知
  - 外部送信なので、MmmBatch の機能として作る
  - 他の人の作成・更新の通知とは別。その仕様は [specs/notes.md](specs/notes.md) の「他の人の作成・更新の通知」にある。

## Web 版 (MmmToolWeb)

- 概要:
  - 複数人が iPhone のホーム画面のアプリから使う Web 版で、ノートとメッセージとリマインダーだけを出す
  - ログインはパスキーだけで、パスワードを持たない
  - 外への公開は Tailscale Funnel
  - 仕様は [specs/web.md](specs/web.md)
  - 構成は、次の 4 つ
    - `src/App/MmmToolWeb/Server`: 常駐する EXE。画面を配り、API を受ける。
    - `src/App/MmmToolWeb/Client`: ブラウザで動く Blazor WebAssembly の画面
    - `src/Auth/MmmToolWeb.Auth.Core`: パスキーの手続き
    - `src/Data/MmmToolWeb.Data.SqlServer`: 認証用の表の保存
- ログインの本番の作り: 「ログインの世代の番号」を上げて、その人のログインを全部無効にする操作 (Cookie に番号を入れて、確かめる)、ログイン画面の文言
- リマインダーの Web 画面を作る
  - 機能ごとにプロジェクトを足す。ノートの Web 版 (`MmmToolWeb.Notes` と `MmmToolWeb.Notes.Client`)と同じ形で、サーバー本体と画面の本体には、登録の 1 行ずつだけを足す。
  - 作るとき、DB のログイン `MmmToolWeb` に、リマインダーの表の権限を足す。権限は、表の名前と権限を一覧にして、許可をもらってから付ける。
  - 「まとめて追加」も同じ画面に置く
    - 時刻の表で会議を選ぶ形にする。鳴らす時刻と件名を作る処理は、PC 版と同じ `MmmTool.Reminders.Core` の `ReminderDraft` を使う。仕様は [specs/reminders.md](specs/reminders.md) の「まとめて追加」にある。
    - iPhone の画面の幅に合わせて、表と一覧の置き方を決める。
    - 画面側から `.Core` を使えるようにする件が先に要る。下の項目を見る。
  - 画面の案
    - 「今日」: 今日の対象を時刻の順に並べ、各行の未・スヌーズ・完了を指で切り替える。完了は下に折りたたむ。
    - 「すべて」: 全部を日付の順に並べる。「過去の予定を表示」「削除済みを表示」と、編集・コピーして新規追加・削除。
    - 入力: 画面全体のシート。件名・備考・リンク・日付か曜日・時刻・宛先・送信先。
  - ユーザーに決めてもらうこと (おすすめつき)
    - 「今日」と「すべて」の切り替え: 画面の上に 2 つの切り替えを置き、開いたときは「今日」にする。
    - 「通知を読み上げる」: iPhone の入力には出さない。編集しても、入っている値は変えない。新規はオフで保存する。
    - 「送信先」: 選べるようにする。自分が登録した ntfy・Discord・Web 版通知を選べば、同じ経路で通知される。
- 通知から、リマインダーの「完了」や「スヌーズ」を押せるようにする
  - リマインダーの Web 画面を作るときに、受け取った通知の画面のリマインダーに足す。
- Web 版通知の公開鍵を、MmmBatch の画面で見られて、コピーできるようにする
  - 公開鍵は、MmmBatch が起動時に作り、`Data/AppSettings.json` の `WebPush.PublicKey` に置く。Web 版のサーバーの設定 `Web:PushPublicKey` へ写すとき、設定ファイルを開かずに済むようにする。
- Web 版通知で、番号を覚えていない端末の行を、Web 版の画面から削除できるようにする
  - 端末の記憶が消えると、登録の行が残る。この行の削除は、MmmTool の送信先の一覧で行う。
- 独自ドメインへ替えるとき: パスキーは、アドレスに結びつくため、全員が登録し直す
- 画面側の共通の CSS を 1 か所にまとめる
  - 文字のボタン (`.text`)・メッセージ (`.message`)などが、`NotesPage.razor.css` と `NoteEditSheet.razor.css` に、同じ内容で書き写されている。
  - 方向: `MmmToolWeb.Features.Client` に共通の CSS か部品を置き、機能の画面は、それを使う。
- 画面側から `.Core` の判定を使えるようにする
  - メッセージの「送信」を押せる条件が、画面側 (`MessagesPage.razor`)と `MessageDraft.CanSend` の 2 か所にある。
  - 方向: 判定を 1 か所にして、画面側でも使う。AnyCPU の `.Core` を作るか、判定の結果をサーバーが返す形を、案にして決める。
- メッセージの Web 版に、送信先の登録の画面を足す。
  - 送信先の登録の画面は MmmTool の設定ページにあり、iPhone だけで使う人は、宛先を足せない。受け取り先の指定は、Web 版の設定の画面で行える ([specs/web.md](specs/web.md) の「設定 (Web 版)」)が、送信先を足す画面は無い。
  - 方向だけ。送信先の値 (トピック名・Webhook の URL)は秘密なので、Web 版で読めるようにするかどうかも、決めてから作る。
- 画面のファイルの配り方を、ファイル名に版が付く方式の `MapStaticAssets` に替える。
  - すべての画面のファイルに `Cache-Control: no-cache` を付けて、毎回、更新の有無を確かめさせている。
  - 方向: 版が付く方式にして、確かめる通信をなくす。
- `browser.js` の `getLastFeature`・`setLastFeature` を消す
  - 起動時はいつもホーム画面を開くようにしたので、最後に開いた機能を覚える処理は、画面側から外した。関数だけが、端末に残る古い画面のために残っている。
  - 方向: 古い画面が端末に残らなくなったことを、利用者に確かめてから、消す。`MapStaticAssets` に替えれば、古い画面が残る場面が無くなる。

## 不具合の調査

- CLI補助の下の入力欄にテキスト (1 行)を貼ると固まり、しばらくして落ちる
  - CLI補助の入力欄で起きる。ほかの画面の入力欄で起きるかは、調べる項目。
  - アプリのログ (`Data/Logs`)には何も残らず、エラーのダイアログも出ない
  - Windows のイベントビューアー (Windows ログ → アプリケーション)には、`Application Error` (イベント ID 1000)が残る
    - 障害が発生したモジュールは `Microsoft.UI.Xaml.dll`、例外コードは `0xc000027b` (WinUI 内部の失敗による強制終了)
  - 失敗の中身を見るため、落ちたときのダンプを取る。管理者の PowerShell で、次を順に実行する。
    - `cd "HKLM:\SOFTWARE\Microsoft\Windows\Windows Error Reporting\LocalDumps"`
    - `New-Item MmmTool.exe` (`LocalDumps` が無いときは、先に `New-Item LocalDumps`)
    - `cd MmmTool.exe`
    - `New-ItemProperty . -Name DumpFolder -Value D:\CrashDumps -PropertyType ExpandString`
    - `New-ItemProperty . -Name DumpType -Value 2 -PropertyType DWord`
  - 設定すると、`MmmTool.exe` が落ちるたびに、`D:\CrashDumps` に `MmmTool.exe.<番号>.dmp` ができる。完全なダンプなので、入力した文章やパスワードが入る。人に渡さない。中身は WinDbg で見る。
  - 調べ終わったら、管理者の PowerShell で `cd "HKLM:\SOFTWARE\Microsoft\Windows\Windows Error Reporting\LocalDumps"` → `Remove-Item MmmTool.exe` で設定を消し、`D:\CrashDumps` のダンプを手で消す
  - 原因が分かって直したら、この項目ごと消す

- 通知をクリックしても、開いたままのリマインダーの画面が、他のウィンドウの後ろに残ることがある
  - 症状の出る端末と出ない端末がある。同じ端末でも、前面になるときと、ならないときがある。DB モード。
  - 原因の見立て: `BringToFront` の `SetForegroundWindow` が Windows に断られている。通知ウィンドウはフォーカスを奪わない作りで、クリックから前面化までに、Low 優先度の待ちが 2 回入る。
  - 調べるために、前面化の結果を `Data/Logs/foreground-yyyy-MM-dd.log` に書く計測を入れてある。
    - 計測のコード: `WindowExtensions.BringToFrontTraced`・`ReminderWindowService.WriteTrace`・`ReminderWindowService.MarkClicked`
    - コメントに `TEMP(計測)` と書いてある。
  - 症状の出る端末で、リマインダーの画面を開いたまま通知をクリックして、ログを見る。
    - `SetForegroundWindow=False`: 前面化の権利が無い。待ちを挟む前の、クリックの処理の中で前面化する作りにする。保険として、Windows App SDK 2.1.9 にある `AppWindow.MoveInZOrderAtTop` を使う。
    - `SetForegroundWindow=True` なのに後ろに残る: 前面化のあとに、通知を閉じる `CloseAfterClick` か作り直しの `Present` が、前面を奪っている。順序を見直す。
  - 直したら、計測のコードとログを消し、`BringToFront` に戻す。`NativeMethods.txt` の `GetWindowThreadProcessId` も、使わなければ消す。
  - 閉じている画面を新しく開く経路は `Activate()` だけで `BringToFront` を通らない。docs/specs/reminders.md の「前面に出すのは SDK の `BringToFront`」と食い違うので、直すときにそろえる。

## そのうち (方向だけ)

- `scripts/` の発行用のショートカット (`9001_tool_publish`・`9011_web_publish`・`9021_batch_publish`・`9029_admin_publish`)を、リポジトリに入れるか、`.gitignore` に足すかを決める
  - 場所がこの PC のものを指す。コミットには入れていない。`.gitignore` に規則は無い。
  - 方向: `.gitignore` に足す。

- ノートの Web 版の本文の入力に、書式のボタンの列を足す
  - 入力欄の上に、太字・斜体・下線・取消線・色・リンクのボタンを並べる
  - iPhone には右クリックが無く、タグを手で書くのは難しいため
- Web 版の画面を、スマホ向けのサイトとして、あらためて見直す
  - 方向: 使いやすさを第一に、触って分かる操作と、今のスマホアプリの標準に合わせる
  - 見直しの基準は、`~/.claude/skills/mobile-ui/` のスキル `mobile-ui`。画面ごとではなく、全体として見直す。
  - ノートの画面で、基準に合っていない所の候補
    - 押したときの反応が無い。ボタンに、押している間の見た目の変化を付ける。
    - 「編集」と「削除」が、すき間なく並ぶ。押せる部品の間は 8 ピクセルあける。
    - 日付の移動 (前の日・次の日・今日)が、画面の上にある。毎日使う操作なので、画面の下の操作の列へ移す案がある。
    - ホームへ行って戻ると、表示している日が今日に戻る。戻ったときは、日付を保つ。
    - 読み込みに失敗したとき、その場所に「もう一度」のボタンが無い。
    - 開閉の矢印の動きが、「視覚効果を減らす」の設定で止まらない。
    - 重要度の帯が、色だけで区別されている。`mobile-ui` は「色だけで意味を伝えない」とし、[specs/notes.md](specs/notes.md) は「中・低・最低は帯を付けない」としていて、ぶつかる。どちらを優先するかを、ユーザーが決める。
  - メッセージの Web 画面も、同じ基準で見比べる。
- サーバー側のビルド (GitHub Actions)で、タグを付けたら発行からリリースまで自動で行う。ユーザーが試したがっている。リポジトリは公開で、Actions は無料。
  - 調べること: 手元と同じ配布物になるか、`dotnet publish` の時間、SDK の取得
  - 最初は、zip を実行結果の添付に置くだけの手動実行で試す
- 括弧の書き方を、既存のファイルすべてで直す
  - 閉じ括弧の直後の句読点・全角の括弧・開き括弧の前のスペースが、docs・README・ソースのコメントや文言に残っている
  - 洗い出しは `py -I ~/.claude/tools/check-rules.py --parens <ファイル>...`。1 行ずつ違反の場所が出る
  - 新しく書く行は、ファイルを編集した直後の自動チェックが止める。直すのは、既存の行だけ
  - アプリ・SDK の両方のリポジトリが対象
- MmmBatch を MmmToolBatch に改名する
  - アプリの名前を MmmTool・MmmToolWeb とそろえるため
  - 直す範囲は、フォルダ・プロジェクト・名前空間・文書・DB のログイン名
- CLI補助を、サイドバーから別のウィンドウに外せるようにする
  - 方向: 動いているシェルは残し、ターミナルの表示 (WebView2)だけを、外した先で作り直す
  - 理由: WebView2 は、作られたウィンドウから別のウィンドウへ移すと、入力が元のウィンドウへ届いてしまうため。ページごと作り直すと、シェルが終了する。
  - 作り方の案
    - 画面の中身は引き継がない。外した先では、以降の出力から表示する。画面を描き直し続ける表示 (Claude Code など)は、ターミナルの大きさを変えて描き直させる。
    - 画面の中身も引き継ぐなら、xterm.js の `addon-serialize` を SDK に足して、外す前に保存し、外した先で復元する。MIT ライセンスで、同梱の一覧にも足す。
  - 直す場所は、`CliSessionView`・`CliAssistPage` と、SDK の `PageDetacher`・`TerminalControl`
  - 登録は `AddNavigationPage` の `canDetach: true` で決める ([specs/tray-and-main.md](specs/tray-and-main.md))
- メッセージの会話の切り替えで入れた「重ねたフェード」を、アプリ全体の切り替えに広げる。共通の処理にできるかを検討する
  - 方向: 画面の一部の中身を入れ替えるとき、前の見た目を画像に撮って重ね、下で差し替えてから、薄くしながら新しい中身を濃くする。空の瞬間を見せない。
  - 仕組みは、メッセージのページ (`MessagesPage.xaml.cs` の `PrepareSwitchAsync`・`CrossFadeAsync`・`FadeAsync`)と、ViewModel の `PrepareSwitchAsync`・`MessagesReplaced` にある。決めたことは [specs/messages.md](specs/messages.md) の「人の一覧」にある。
  - 検討すること
    - SDK の `MmmSdk.WinUI` に、部品 (切り替え用のコンテナ、または添付の仕組み)として置けるか
    - 切り替えが要る画面の候補を洗い出す (別の中身に入れ替わる一覧・詳細・ページ)
    - 長さと動きの形を共通の値にそろえる。いまは 500 ミリ秒
    - 「視覚効果を減らす」の設定のとき、動きを止めるか
    - 画像を撮る方式の限界 (撮れない部品・大きな画面のメモリ)

## ユーザーが用意するもの待ち

- 管理ツール (MmmToolAdmin)のアイコン: いまは MmmBatch のアイコンを仮に使っている (`src/App/MmmToolAdmin/Assets/app.ico`)
