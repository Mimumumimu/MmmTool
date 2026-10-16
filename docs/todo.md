# 未実装・残りの作業

これからやることだけを書く。実装したら消し、説明は該当の `docs/` に移す。実装済みの機能は `docs/specs/` を見る。

## 作業管理

設計は [specs/work-items.md](specs/work-items.md)。

- コードマスタ ([specs/work-items.md](specs/work-items.md) の「コードマスタ」): 残りは、コードマスタの管理ツール。別ツールにする (選択肢の追加・外す・並べ替え。ローカルモードの扱いは未定)
- 後で決める: リーダー的な人の更新の権限 (SQL Server の実装のとき)・リマインダーを SQLite に移すか (実データがあり、移行が要る)
- 方向だけ決めている後回し: ガントチャート (別の画面。同じデータを読む)・Backlog との連携 (状態・優先度・進捗度を数値で対応づける)

## 不具合の調査

- CLI補助の下の入力欄にテキスト (1 行)を貼ると固まり、しばらくして落ちる。CLI補助の入力欄だけで起きる (ほかの画面の入力欄では確かめていない)。アプリのログ (`Data/Logs`)には何も残らず、エラーのダイアログも出ない
  - Windows のイベントビューアー (Windows ログ → アプリケーション)には、`Application Error` (イベント ID 1000)が残る。障害が発生したモジュールは `Microsoft.UI.Xaml.dll`、例外コードは `0xc000027b` (WinUI 内部の失敗による強制終了)
  - 失敗の中身を見るため、落ちたときのダンプを取る。管理者の PowerShell で、次を順に実行する
    - `cd "HKLM:\SOFTWARE\Microsoft\Windows\Windows Error Reporting\LocalDumps"`
    - `New-Item MmmTool.exe` (`LocalDumps` が無いときは、先に `New-Item LocalDumps`)
    - `cd MmmTool.exe`
    - `New-ItemProperty . -Name DumpFolder -Value D:\CrashDumps -PropertyType ExpandString`
    - `New-ItemProperty . -Name DumpType -Value 2 -PropertyType DWord`
  - 設定すると、`MmmTool.exe` が落ちるたびに、`D:\CrashDumps` に `MmmTool.exe.<番号>.dmp` ができる。完全なダンプなので、入力した文章やパスワードが入る。人に渡さない。中身は WinDbg で見る
  - 調べ終わったら、管理者の PowerShell で `cd "HKLM:\SOFTWARE\Microsoft\Windows\Windows Error Reporting\LocalDumps"` → `Remove-Item MmmTool.exe` で設定を消し、`D:\CrashDumps` のダンプを手で消す
  - 原因が分かって直したら、この項目ごと消す

## そのうち (方向だけ)

- サーバー側のビルド (GitHub Actions)で、タグを付けたら発行からリリースまで自動で行う。ユーザーが試したがっている。リポジトリは公開で、Actions は無料
  - 調べること: 手元と同じ配布物になるか、`dotnet publish` の時間、SDK の取得
  - 最初は、zip を実行結果の添付に置くだけの手動実行で試す

## ユーザーが用意するもの待ち

- README.md の画像: 画像はユーザーが用意する (リンクの中身などがまだ決まっておらず、撮れない)。届いたら `docs/images/` に下の名前で置き、「主な機能」の各機能の見出しの下に添える (CLI補助の `cli-assist.jpg` と同じ形)
  - 作業管理の表に実績を入れている画面 `work-items.png`・リマインダーのメイン画面 `reminders.png`・通知ダイアログ `notification.png`・リンクの編集画面 `links.png`・トレイの「リンク」メニュー `tray-links.png`
  - README.md の、画像を置く場所には `<!-- TODO(画像): … -->` の印がある。画像を置いたら、印を外して `![説明](docs/images/名前.png)` にする
- 冒頭 (ツカミの下)には、特定の機能に偏らない画像だけを置く。CLI補助はメイン機能ではなく、便利機能の詰め合わせの 1 つなので、CLI補助の画像を冒頭に置くと、CLI補助のアプリに見える
  - 候補はトレイのリンクメニューと通知ダイアログ (常駐していて邪魔にならないことが伝わる)。どれを冒頭に置くかは、画像が届いたときにユーザーと決める
