# CLI補助

AI のコマンドラインツール（Claude Code・Kiro など）を使うときの補助画面。ターミナルの下に送信欄を置き、定型コマンドの送信・送信履歴・添付を助ける。`Features/CliAssist/`（中は入口の画面の `Main/` と、作業ディレクトリ変更ダイアログの `WorkingDirectory/`、初期設定ダイアログの `Setup/` に分けている。Core 側は `MmmTool.Core/CliAssist/`。ターミナルとシェルの決定は SDK）。

## 画面
- 3 領域のレイアウト：左に定型コマンドのツリー、中央にターミナル、下に送信欄
- ターミナルは SDK の `TerminalControl`（ConPTY + xterm.js（WebView2））。入出力・リサイズに対応し、シェルが終了したあとは何かキーを押すと再起動する。セッション・画面・WebView2 の守り・出力の流量制御・シェルの決定（PATH 上の pwsh.exe、無ければ Windows PowerShell）・シェル別の作業ディレクトリ変更コマンドは、SDK の `docs/terminal.md`（`external/MmmSdk/docs/terminal.md`）
  - アプリ側の使い方: `ITerminalSession`（SDK）を `PseudoConsoleSession` として DI に Transient で登録し、Host の破棄時に Dispose する（シェルも終了する）。画面は `CliAssistPage.xaml` の `TerminalControl` に `CliAssistViewModel.Terminal` を渡す
  - ターミナルが起動していない（起動の直前・再起動の途中・WebView2 を初期化できなかった）ときと、シェルが終了しているときは、定型コマンド・入力欄の送信を捨てずに、InfoBar で知らせる（`CliAssistViewModel.TrySubmit`）。送れなかったときは、入力欄・添付を残し、タブの切り替え・フォーカスの移動もしない。作業ディレクトリ変更は、送れなくても、これから（再）起動するシェルの作業ディレクトリと履歴は更新する
  - 起動時の準備（`CliAssistStartup`）で、既定のシェルの探索（`ShellLocator.Default`。PATH の全項目へ触れる）をバックグラウンドで済ませる。UI スレッドで初めて探して止まらないようにするため
  - 補助スクリプトを動かすシェルは、ターミナルで使うシェルに合わせる（`ShellLocator.Default.FileName`。シェルの中でシェル自身を呼ぶので、ファイル名だけを使う）
  - 作業ディレクトリ変更: `ShellCommands.TryChangeDirectory(Terminal.Shell, …)`。cmd でパスに `%` があると作れない（`false`）ので、移動せずにエラー表示する
  - xterm.js のファイル（`Assets/Terminal/`）は SDK の csproj が出力フォルダーへ配る。アプリの csproj には書かない
- 作業ディレクトリ変更ダイアログ（最近使ったフォルダの履歴・フォルダ選択・存在確認）
  - 存在確認（`Directory.Exists`）は、ネットワークパスで止まることがあるため、UI スレッドの外で行う。入力欄の変更は少し待ってから確認する（SDK の `Debouncer`。リンク編集のパスの種類の調べ方も同じ）。起動時の作業ディレクトリ（`CliSettingsService.StartDirectory`）も、読み込みの中でバックグラウンドで確認し、存在するときだけ使う

## 初期設定（使うツール・環境）
使う AI ツール（Claude Code / Kiro。両方も可）と、動かす環境（Windows（PowerShell）/ WSL）を選べる。決めた理由は [../decisions/0016-wsl.md](../decisions/0016-wsl.md)（環境）・[../decisions/0017-cli-tools.md](../decisions/0017-cli-tools.md)（ツール）。
- 選ぶのは、定型コマンドのファイル（`Data/CliCommands.json`）を作るとき（無い・壊れていた）と、初期化するときだけ。初期設定ダイアログ（`Setup/CliSetupDialog`。ツールはチェックボックス・環境はラジオボタン。ツールを 1 つも選ばないと決定できない）
  - 初回（CLI補助のページを開いたとき）: 題名「CLI補助の初期設定」・ボタン「作成」だけ。キャンセルは無く、Esc でも閉じない（ファイルを作るのに、選んだ内容が要るため）。最初は Claude Code・Windows を選んだ状態
  - 選んだツールは、定型コマンドのフォルダとして作る（下の「定型コマンド」）。ツールは別には保存しない（フォルダの中身そのものなので）。環境は、ファイルの `"environment": "windows" | "wsl"` に書く（省略・この項目が無い古いファイルは Windows。知らない値は Windows として動かし、InfoBar で知らせる）
  - 変えるときは、左ペインの「⋯」（その他）メニューの「定型コマンドを初期化…」を選ぶ（めったに使わない・取り消せない操作なので、常に見えるボタンにせず、メニューに入れる。`CliAssistViewModel.ResetCommandsAsync`）。初期設定ダイアログが、題名「定型コマンドを初期化」・ボタン「初期化」「キャンセル」（既定のボタンは置かない。取り消せない操作なので Enter で実行させず、キャンセルを既定にして強調色にすると主な操作に見えるため。SDK の確認ダイアログと同じ）で開き、消えるもの・終了するものの警告（InfoBar）を出す（確認のダイアログを別に重ねず、1 つで済ませる）。あとで変える方法の案内は、初回だけ出す（初期化のときは、今まさにその方法で開いているため）。環境は今の環境を選んだ状態から始める。決定すると、その既定で `CliCommands.json` を上書きする（今の内容は引き継がず、退避もしない。ユーザーの決定。Repository の `SaveAsync`）
    - ターミナルは、環境が変わらなくても、いつも起動し直す（ユーザーの決定。SDK の `TerminalControl.RestartSessionAsync`。起動し直すには端末の大きさが要るので、ViewModel は `TerminalRestartRequested` で View に頼む）。タブはシェル側に戻す
    - 環境が変わったときは、添付をすべて外す（パスの形・一時保存先が環境ごとに違うため）。一時保存した添付は、切り替える前の環境の保存先から消す。入力欄の本文は残す
  - アプリを終了してから `Data/CliCommands.json` を消しても、次に開いたときに選び直せる
- 環境で決まるもの
  - ターミナルで起動するシェル: Windows は既定のシェル（`ShellLocator.Default`）、WSL は `wsl.exe`（`ShellLocator.Wsl`。既定のディストリビューションの既定のシェル）。定型コマンドを読み込んでから決めるので、ターミナルは読み込み（`CliAssistViewModel.InitializeAsync`）が済んでから画面につなぐ（`CliAssistPage.OnLoaded`。つないだときに起動する）
  - 既定の定型コマンド: 違うのは Claude Code の「会話履歴の削除」の補助スクリプトだけ（下の「補助スクリプト」）
  - 貼り付けた画像の一時保存先（下の「添付」）
- 設定でオフにできる（[settings.md](settings.md) の「機能のオン・オフ」）。オフにすると、ページ（ターミナルのセッション・入力欄・添付の一覧）を捨てて、シェルとその中の CLI を終了する。オンに戻すと、ページとセッションを新しく作る（入力中の内容・添付は引き継がない。一時保存した添付は、終了時の掃除で消える）。起動時にオフなら、CLI補助の準備（利用状態の読み込み・シェルの探索）も行わない
- 今どちらの環境で動いているかは、ターミナルのシェルの種類（`Terminal.Shell.Kind`）1 か所で見る
- WSL では、シェルへ渡すパスを Linux の形にする（SDK の `ShellCommands.TryConvertPath`・`WslPath`。`D:\work` → `/mnt/d/work`、`\\wsl.localhost\Ubuntu\tmp\a.jpg` → `/tmp/a.jpg`）。画面（作業ディレクトリ変更ダイアログ・添付の一覧・履歴）は Windows のパスのまま扱い、送るときだけ変換する
  - 作業ディレクトリ変更: `cd -- '/mnt/d/...'`（SDK の `ShellCommands.TryChangeDirectory`）。WSL から開けないフォルダー（`\\server\share` などのネットワークのフォルダー）は、移動せずにエラー表示する。シェルの再起動時の開始位置（`Terminal.WorkingDirectory`）は Windows のパスのまま（wsl.exe が変換する）
  - 添付: 送るときに変換する。WSL から開けない場所のファイルは、添付するときに断ってエラー表示する（送ってから読めないと分かるより、先に分かるほうがよいため）
  - 定型コマンドの `{AppDir}`: WSL では `/mnt/d/...` に展開する
- 制約: WSL での動作は、ユーザーの WSL の環境ができるまで、実機で確かめられていない（[../decisions/0016-wsl.md](../decisions/0016-wsl.md)）

## 定型コマンド
- 「シェル」と「AI セッション」の 2 タブ固定（「ターミナル」は中央のペインの名前で、タブとは別。ターミナルは「シェルのタブ」「AI セッションのタブ」のどちらのコマンドも受け取る）。最初は全部開いた状態（開閉は自由。タブを切り替えると開いた状態に戻る）
- 左ペインは幅 280px。長い名前は横スクロール（TreeView 内の ScrollViewer に Loaded で設定する）
- JSON は `shell` / `session`（列挙型は `CommandCategory.Shell` / `Session`）。どちらのタブも、初期設定で選んだツールごと（Claude Code・Kiro）にフォルダで分ける（並びは Claude Code → Kiro）。作業ディレクトリ変更はシェル側の先頭
- 既定のコマンド（`CliCommandDefaults`）。ツールどうしで、同じ働きのコマンドは同じ表示名・同じ順にそろえる。Kiro のコマンドは公式ドキュメント（kiro.dev の CLI commands・Slash commands）で確かめた

  | 表示名 | Claude Code | Kiro | 備考 |
  | --- | --- | --- | --- |
  | 起動 | `claude` | `kiro-cli chat` | |
  | 続きから再開 | `claude --continue` | `kiro-cli chat --resume` | |
  | 最新化 | `claude update` | `kiro-cli update` | |
  | 会話履歴の削除 | 補助スクリプト（下） | `kiro-cli chat --resume-picker`（表示名は「会話履歴の削除（一覧で Ctrl+D）」） | Kiro は履歴を 1 つの SQLite（`~/.kiro/`）に持つので、ファイルをごみ箱へ送る方式は使えない。Kiro 自身のセッション一覧で Ctrl+D で消す（戻せない）。制約: この一覧で Ctrl+D が効くかは実機で未確認 |
  | 新規チャット | `/clear` | `/chat new` | Kiro の `/clear` は同じ会話のまま中身を消すだけなので使わない |
  | 読み込みファイル一覧 | `/context` | `/context` | |
  | 会話要約（コンテキスト圧縮） | `/compact` | `/compact` | |
  | CLAUDE.md を作成 / steering を作成 | `/init` | 作成を頼む文 | Kiro の CLI に `/init` は無いので、`.kiro/steering/` に product.md・tech.md・structure.md（Kiro の IDE の「Generate Steering Docs」と同じ 3 つ）を作るよう頼む文を送る（ユーザーの決定） |
  | モデル切替 | `/model` | `/model` | |
  | 会話履歴と再開 | `/resume` | `/chat resume` | |
  | コスト確認 / 使用量の確認 | `/cost` | `/usage` | Kiro は料金・残りクレジットを出す |
  | 終了 | `/exit` | `/quit` | |
- 葉のコマンドの設定（値は列挙型 `CommandCategory` / `FocusTarget`（Core）に変換する。`CliCommandNode.GetSwitchTo()` / `GetFocus()`。手で編集した JSON なので、大文字小文字は区別せず、知らない値は読み込みでは無視する。ただし、書き間違いに気づけるよう、読み込んだあとに `CliCommandSet.Validate()` で調べ、誤りを画面の警告（InfoBar）に出す）
  - `"switchTo": "shell" | "session"`：送信後にそのタブへ切り替える。既定では「起動」「続きから再開」→ AI セッション、「終了」→ シェル
  - `"focus": "terminal" | "input"`：送信後のフォーカスの移動先（input = 送信欄）。既定では、起動・新規チャット → input、モデル切替・会話履歴と再開 → terminal（一覧から選ぶ操作でキーを使うため）
- 文字列中の `{AppDir}` は、送信時に EXE のフォルダへ展開する（`CommandPlaceholders`）
- 既定のコマンドを増やしても、生成済みの `Data/CliCommands.json` には反映されない（無いときだけ生成）。反映するには「定型コマンドを初期化…」を選ぶ（上の「初期設定（使うツール・環境）」）
- 既定のコマンドの定義はアプリ側（`Features/CliAssist/CliCommandDefaults`）にあり、Core の `JsonCliCommandRepository` には「既定を作る処理」（初回の初期設定ダイアログを開いてから、その既定を作る。非同期）を DI で渡す（Core がアプリの配置と画面を知らないようにするため）。Windows では、補助スクリプトを動かすシェルを、ターミナルで使うシェル（`ShellLocator.Default`）に合わせる（pwsh が無い環境でも動く）

## 送信
- 送信は Ctrl+Enter。`ITerminalSession.Submit` → xterm.js の `term.paste`（ブラケットペースト）で貼り付け、そのあと Enter で確定する。複数行でも CLI がひとまとまりで受け取る
- Enter を送るタイミングは、貼り付け後に一度出力があり、それが 200ms 途切れたとき（最低 150ms・最長 2 秒）。決めた理由は [../decisions/0007-submit-enter-wait.md](../decisions/0007-submit-enter-wait.md)
- 送信履歴：Flyout に最大 50 件・絞り込み付き。保存せず、メモリ上だけに持つ（`SendHistory`。Core）。空白のみは無視し、同じ本文は先頭へ移す。絞り込みは前後の空白を無視し、大文字小文字を区別しない

## 送信欄
- 入力欄は 3 行分の高さで固定する（超えた分は欄内スクロール）。入力で伸びるとターミナルが縮み、CLI が全画面を描き直してちらつくため
  - 固定値だとフォントによって下だけ余る・欠けるので、読み込み時に「1 行の実測の高さ × 3 行 + 余白・枠」で決める（`FitInputBoxToThreeLines`。XAML の 64px は初期値）
- 入力欄にフォーカスが来たら IME をオンにする（SDK の `ImeControl.TurnOn`。日本語をすぐ打てるように）
- 添付の行（高さ 48px）は、添付があるときだけ出す（高さが変わるのは最初の添付時と送信時だけ）
- 操作の説明はプレースホルダーに入れる
- エラーの InfoBar は、レイアウトに場所を取らず、ターミナルの上部に重ねて表示する

## 添付
- 画像は Ctrl+V で貼り付け（JPEG に変換）。ファイルはドラッグ＆ドロップまたは貼り付け（エクスプローラーでコピーしたもの）。フォルダーは添付しない
- 元がディスク上のファイルかどうかで扱いを分ける（`AttachmentItem.IsTemporary`）
  - **ディスク上のファイル**（ドロップ・貼り付けしたファイル）: コピーせず、元のパスをそのまま送る（`CliAssistViewModel.AddAttachmentFile`）。ローカルで動く CLI は元の場所のファイルを直接読めるので、置き場所（どのプロジェクトのファイルか・隣のファイル）も AI の手がかりになる。今どきのローカルの AI ツール（VS Code の Copilot Chat など）と同じ形。取り除く（× ボタン）ときは一覧から外すだけで、元のファイルは決して消さない
  - **ファイルではないもの**（クリップボードの画像）と、**パスの無いファイル**（メールの添付ファイルなど、ディスク上に無いもの。`StorageFile.Path` が空）: 一時保存して、そのパスを送る（`AddAttachmentImageAsync` / `AddAttachmentContentAsync`）。パスの無いファイルのドロップは、制約: 実機で未確認（送り元のアプリが手元に無い）
- WSL の環境では、一時保存先を WSL の /tmp（`\\wsl.localhost\<既定のディストリビューション名>\tmp\MmmTool\session_日時\`。CLI へは `/tmp/MmmTool/...` で渡す）にする（SDK の `AttachmentStore.ForWsl`。DI は環境ごとのキー付きの Singleton で、ViewModel が今の環境のほうを使う）。終了時の削除・古いものの掃除はしない（ユーザーの決定。WSL の /tmp は、systemd が有効なら WSL の起動のたびに空になる）。送信前に × で取り除いたものは、すぐ削除する。ディストリビューションが見つからない（WSL が入っていない）ときは、添付が失敗して InfoBar に出る
- Windows の環境では、一時保存は `%TEMP%\MmmTool\session_日時\` に連番で保存する（SDK の `AttachmentStore`。フォルダ名はアプリ側が `"MmmTool"` を渡す）。送信前に取り除いたものはすぐ削除する。送信後は一覧だけ空にし、ファイルは終了時に削除する（`AttachmentStore.Dispose`）。1 日より古い残りは、次回の初回添付時に削除する
  - フォルダは Debug / Release など別の場所の EXE と共有するので、同時に動いている別ビルドの 1 日より古いセッションも消える。これは想定内（ユーザーの決定）。添付は CLI が読み込めば用済みの一時ファイルで、元は読み込んだらすぐ消す仕様だったものを緩めて残しているだけのため。EXE ごとに分けるとパスが長くなるだけなので、分けない（SDK の `docs/controls.md`）
- 送信時は、本文のあとに指示文と各ファイルの絶対パス（1 行ずつ）を付ける（Core の `SendText.Compose`）
  - 指示文は「次のファイルも参照してください（バイナリでも読めるものは読む）。これらのファイルを変更するのは、上記の指示で求められたときだけにしてください」。元の場所のファイルを渡すので、AI が頼まれていないのに書き換えないようにする。ただし「このファイルを直して」のように頼んだときは直してほしいので、一律には禁じず、本文の指示に従わせる
  - 本文が無い（空白だけ）ときは、指す先の「上記の指示」が無いので、「上記の指示に関連して」「上記の」を外した言い回しにする
- サムネイルは SDK の `ThumbnailImage.FromFile`（ファイルを開いたままにしない＝削除できなくならないよう、中身をメモリに読み込んでから表示する）。JPEG への変換も SDK の `IImageConverter`（詳細は SDK の `docs/controls.md`）

## 補助スクリプト：会話履歴の削除（Claude Code）
Kiro の「会話履歴の削除」は補助スクリプトを使わず、Kiro 自身のセッション一覧を開く（上の「定型コマンド」の表）。
- `Assets/Tools/Remove-ClaudeSession.ps1`（Windows）・`Assets/Tools/Remove-ClaudeSession.py`（WSL）：Claude Code の会話履歴を矢印キーで選んでごみ箱へ送る（`~/.claude/projects` 配下の `.jsonl` が対象）。2 つは同じ流れ・同じ操作にしている（直すときは両方を直す）
- 出力フォルダへコピーされ、定型コマンド「会話履歴の削除」（シェル › Claude Code）から呼ぶ。Windows は `<シェル> -NoProfile -File "{AppDir}\Assets\Tools\Remove-ClaudeSession.ps1"`、WSL は `python3 "{AppDir}/Assets/Tools/Remove-ClaudeSession.py"`（`{AppDir}` は `/mnt/d/...` に展開される）
- WSL 版（Python 3）
  - WSL には Windows のごみ箱が無く、PowerShell 版を WSL の履歴（`\\wsl.localhost\...`）に向けると、ネットワークのパスなので戻せない形で消えてしまうため、別に作った。Linux の標準のごみ箱（freedesktop.org の Trash。`~/.local/share/Trash` の `files/` と `info/*.trashinfo`）へ移す。戻すときは、`files/` から元の場所（`.trashinfo` の `Path`）へ移す（trash-cli の `trash-restore`・`gio trash --restore` でも戻せる）
  - Python 3 は Ubuntu に標準で入っている。キー入力は端末を raw モードにして読む（`termios`）。Ctrl+C もキーとして受け取る。Esc キー単独か、矢印などの続きかは、50ms 待って見分ける
  - オプションは `--all` / `--what-if` / `--root`（PowerShell 版の `-All` / `-WhatIf` / `-Root`）
  - 改行は LF にする（`.gitattributes` の `*.py text eol=lf`。Windows で取り出しても CRLF にしない）
  - 制約: WSL の環境ができるまで、実機で動かせていない（構文の検査だけ済み）
- PowerShell 版のファイルは日本語を含むので BOM 付き UTF-8（PowerShell 5.1 の文字化け対策）。Python 版は BOM なしの UTF-8
- タイトルの取得は、`.jsonl` を 1 行ずつ文字列で探して最後の `ai-title` 行を使う（`Select-String` のパイプラインを通さない）。制約: PowerShell をこの環境で動かせず、速さと動作は実機で未確認。遅いままなら、ファイルの末尾から読む方式に替える
- 実行中のセッションかどうかは調べない。削除はごみ箱への移動なので、間違えても戻せるため。実行中かを確実に判定する方法（ファイルが開かれたままか）が無いことも理由
- 矢印キーの選択画面は実ターミナルが必要（入力がリダイレクトされているときは番号入力に切り替わる）
- 操作：↑↓ 移動 / Space 選択 / Tab 選択して下へ（Shift+Tab は上へ） / a 全選択 / Enter 決定（未選択ならカーソル行） / Esc・Backspace 戻る / q・Ctrl+C 終了。削除後は同じプロジェクトのセッション選択へ戻る（残りが 1 件も無ければプロジェクト選択へ）
- IME がオンのままだと Space が全角スペース（キーの種類なし・文字だけ）で届き選択できなかったので、全角スペース・全角の a / q も文字から読み替える

## 保存データ
`Data/CliCommands.json`（定型コマンド）・`Data/CliSettings.json`（最終ディレクトリ・履歴 20 件）。どちらもローカル専用で、読み込みに失敗したときは上書きせず InfoBar で知らせる。詳細は [storage.md](storage.md)。

## 後回し
- 今のところなし（Kiro・WSL で動かすことは、上の「初期設定（使うツール・環境）」で対応した）
