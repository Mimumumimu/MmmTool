# 未実装・残りの作業

これからやることだけを書く。実装したら消し、説明は該当の `docs/` に移す。実装済みの機能は `docs/specs/` を見る。

## 作業管理

- 設計は [specs/work-items.md](specs/work-items.md) にある
- コードマスタ ([specs/work-items.md](specs/work-items.md) の「コードマスタ」): 残りは、コードマスタの管理ツール
  - 別ツールにする
  - できることは、選択肢の追加・外す・並べ替え
  - ローカルモードの扱いは未定
- 後で決める: リーダー的な人の更新の権限 (SQL Server の実装のとき)・リマインダーを SQLite に移すか (実データがあり、移行が要る)
- 方向だけ決めている後回し: Backlog との連携 (状態・優先度・進捗度を数値で対応づける)

## ノート

- 仕様は [specs/notes.md](specs/notes.md) にあり、表は [specs/database.md](specs/database.md) の `Note` にある
- 重要度の入力は、5 つを横に並べた選択にしている。ユーザーが使って評価したあと、5 段階のままでよいか (見せ方も含めて)を、ユーザーに聞く。
- そのうち (方向だけ): 検索
  - 終了日が空のノートが増えたときに、日付を覚えていないノートを探すため

## Web 版 (MmmToolWeb)

- 概要:
  - 複数人が iPhone のホーム画面のアプリから使う Web 版で、ノートとリマインダーだけを出す
  - ログインはパスキーだけで、パスワードを持たない
  - 外への公開は Tailscale Funnel
  - 仕様は [specs/web.md](specs/web.md)
  - 構成は、次の 4 つ
    - `src/App/MmmToolWeb/Server`: 常駐する EXE。画面を配り、API を受ける。
    - `src/App/MmmToolWeb/Client`: ブラウザで動く Blazor WebAssembly の画面
    - `src/Auth/MmmToolWeb.Auth.Core`: パスキーの手続き
    - `src/Data/MmmToolWeb.Data.SqlServer`: 認証用の表の保存
- ログインの本番の作り: 「ログインの世代の番号」を上げて、その人のログインを全部無効にする操作 (Cookie に番号を入れて、確かめる)、ログイン画面の文言
- ノートとリマインダーの Web 画面を作る
  - 機能ごとにプロジェクトを足す (使う機能の分だけ作る)
- リマインダーの送信先に「このアプリ」を足し、iPhone のホーム画面のアプリへ Web プッシュで通知する
  - 画面の部分: リマインダーの Web 画面に「通知を受け取る」ボタンを置く。押すと許可のダイアログが出て、その iPhone の宛先が DB に保存される。
  - 送信の部分: ntfy と同じく、MmmBatch の機能 (`src/Plugins/MmmBatch.*`)として作る。外部送信なので、`MmmTool` と `MmmToolWeb.Server` には入れない。
  - 要るもの: 宛先を人ごと・端末ごとに持つ DB の表・通知の鍵 (VAPID 鍵)1 組・アプリ側のサービスワーカー・送信用の NuGet パッケージ (足す前に確認を取る)
  - 先にリマインダーの Web 画面を作る (送信先を選ぶ場所になるため)
- 独自ドメインへ替えるとき: パスキーは、アドレスに結びつくため、全員が登録し直す

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

## そのうち (方向だけ)

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

## ユーザーが用意するもの待ち

- README.md の画像: 画像はユーザーが用意する
  - リンクの中身などがまだ決まっておらず、撮れない
  - 届いたら `docs/images/` に下の名前で置き、「主な機能」の各機能の見出しの下に添える
  - 画像の名前:
    - CLI補助の、冒頭とは別の画像 `cli-assist-demo.gif`
      - Ctrl+V で画像を添付する場面・会話の圧縮をクリックで送る場面の動く画像
      - 冒頭には `cli-assist.jpg` を置いている
    - 作業管理の表に実績を入れている画面 `work-items.png`・リマインダーのメイン画面 `reminders.png`・通知ダイアログ `notification.png`・リンクの編集画面 `links.png`・トレイの「リンク」メニュー `tray-links.png`
  - README.md の、画像を置く場所には `<!-- TODO(画像): … -->` の印がある。画像を置いたら、印を外して `![説明](docs/images/名前.png)` にする。
