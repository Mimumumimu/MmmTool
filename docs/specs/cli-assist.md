# CLI補助

AI のコマンドラインツール（Claude Code・Kiro など）を使うときの補助画面。ターミナルの下に送信欄を置き、定型コマンドの送信・送信履歴・添付を助ける。`Features/CliAssist/`（中は `WorkingDirectory/`・`Attachments/` に分けている。Core 側は `MmmTool.Core/CliAssist/`。ターミナルとシェルの決定は SDK）。

## 画面
- 3 領域のレイアウト：左に定型コマンドのツリー、中央にターミナル、下に送信欄
- ターミナルは SDK の `TerminalControl`（ConPTY + xterm.js（WebView2））。入出力・リサイズに対応し、シェルが終了したあとは何かキーを押すと再起動する。セッション・画面・WebView2 の守り・出力の流量制御・シェルの決定（PATH 上の pwsh.exe、無ければ Windows PowerShell）・シェル別の作業ディレクトリ変更コマンドは、SDK の `docs/terminal.md`（`external/MmmSdk/docs/terminal.md`）
  - アプリ側の使い方: `ITerminalSession`（SDK）を `PseudoConsoleSession` として DI に Transient で登録し、Host の破棄時に Dispose する（シェルも終了する）。画面は `CliAssistPage.xaml` の `TerminalControl` に `CliAssistViewModel.Terminal` を渡す
  - 起動時の準備（`CliAssistStartup`）で、既定のシェルの探索（`ShellLocator.Default`。PATH の全項目へ触れる）をバックグラウンドで済ませる。UI スレッドで初めて探して止まらないようにするため
  - 補助スクリプトを動かすシェルは、ターミナルで使うシェルに合わせる（`ShellLocator.Default.FileName`。シェルの中でシェル自身を呼ぶので、ファイル名だけを使う）
  - 作業ディレクトリ変更: `ShellCommands.TryChangeDirectory(Terminal.Shell, …)`。cmd でパスに `%` があると作れない（`false`）ので、移動せずにエラー表示する
  - xterm.js のファイル（`Assets/Terminal/`）は SDK の csproj が出力フォルダーへ配る。アプリの csproj には書かない
- 作業ディレクトリ変更ダイアログ（最近使ったフォルダの履歴・フォルダ選択・存在確認）
  - 存在確認（`Directory.Exists`）は、ネットワークパスで止まることがあるため、UI スレッドの外で行う。入力欄の変更は少し待ってから確認する（SDK の `Debouncer`。リンク編集のパスの種類の調べ方も同じ）。起動時の作業ディレクトリ（`CliSettingsService.StartDirectory`）も、読み込みの中でバックグラウンドで確認し、存在するときだけ使う

## 定型コマンド
- 「シェル」と「AI セッション」の 2 タブ固定（「ターミナル」は中央のペインの名前で、タブとは別。ターミナルは「シェルのタブ」「AI セッションのタブ」のどちらのコマンドも受け取る）。最初は全部開いた状態（開閉は自由。タブを切り替えると開いた状態に戻る）
- 左ペインは幅 280px。長い名前は横スクロール（TreeView 内の ScrollViewer に Loaded で設定する）
- JSON は `shell` / `session`（列挙型は `CommandCategory.Shell` / `Session`）。セッション側はツールごと（Claude Code・Kiro など）にフォルダで分ける。作業ディレクトリ変更はシェル側の先頭
- 葉のコマンドの設定（値は列挙型 `CommandCategory` / `FocusTarget`（Core）に変換する。`CliCommandNode.GetSwitchTo()` / `GetFocus()`。手で編集した JSON なので、大文字小文字は区別せず、知らない値は読み込みでは無視する。ただし、書き間違いに気づけるよう、読み込んだあとに `CliCommandSet.Validate()` で調べ、誤りを画面の警告（InfoBar）に出す）
  - `"switchTo": "shell" | "session"`：送信後にそのタブへ切り替える。既定では「Claude Code 起動」→ AI セッション、「終了」→ シェル
  - `"focus": "terminal" | "input"`：送信後のフォーカスの移動先（input = 送信欄）。既定では、起動・新規チャット → input、モデル切替・会話履歴と再開 → terminal（一覧から選ぶ操作でキーを使うため）
- 文字列中の `{AppDir}` は、送信時に EXE のフォルダへ展開する（`CommandPlaceholders`）
- 既定のコマンドを増やしても、生成済みの `Data/CliCommands.json` には反映されない（無いときだけ生成）。反映するにはアプリを閉じて JSON を削除する
- 既定のコマンドの定義はアプリ側（`Features/CliAssist/CliCommandDefaults`）にあり、Core の `JsonCliCommandRepository` には「既定を作る処理」を DI で渡す（Core がアプリの配置を知らないようにするため）。補助スクリプトを動かすシェルは、ターミナルで使うシェル（`DefaultShell`）に合わせる（pwsh が無い環境でも動く）

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
- 画像は Ctrl+V で貼り付け（JPEG に変換）。ファイルはドラッグ＆ドロップまたは貼り付け
- `%TEMP%\MmmTool\session_日時\` に連番で保存する（SDK の `AttachmentStore`。フォルダ名はアプリ側が `"MmmTool"` を渡す）。送信後は一覧だけ空にし、ファイルは終了時に削除する（`AttachmentStore.Dispose`）。1 日より古い残りは、次回の初回添付時に削除する
- サムネイルは SDK の `ThumbnailImage.FromFile`（ファイルを開いたままにしない＝削除できなくならないよう、中身をメモリに読み込んでから表示する）。JPEG への変換も SDK の `IImageConverter`（詳細は SDK の `docs/controls.md`）

## 補助スクリプト：会話履歴の削除
- `Assets/Tools/Remove-ClaudeSession.ps1`：Claude Code の会話履歴を矢印キーで選んでごみ箱へ送る（`~/.claude/projects` 配下の `.jsonl` が対象）
- 出力フォルダへコピーされ、定型コマンド「会話履歴の削除」（シェル › Claude Code）から `<シェル> -NoProfile -File "{AppDir}\Assets\Tools\..."` で呼ぶ
- ファイルは日本語を含むので BOM 付き UTF-8（PowerShell 5.1 の文字化け対策）
- タイトルの取得は、`.jsonl` を 1 行ずつ文字列で探して最後の `ai-title` 行を使う（`Select-String` のパイプラインを通さない）。制約: PowerShell をこの環境で動かせず、速さと動作は実機で未確認。遅いままなら、ファイルの末尾から読む方式に替える
- 実行中のセッションかどうかは調べない。削除はごみ箱への移動なので、間違えても戻せるため。実行中かを確実に判定する方法（ファイルが開かれたままか）が無いことも理由
- 矢印キーの選択画面は実ターミナルが必要（入力がリダイレクトされているときは番号入力に切り替わる）
- 操作：↑↓ 移動 / Space 選択 / Tab 選択して下へ（Shift+Tab は上へ） / a 全選択 / Enter 決定（未選択ならカーソル行） / Esc・Backspace 戻る / q・Ctrl+C 終了。削除後は同じプロジェクトのセッション選択へ戻る（残りが 1 件も無ければプロジェクト選択へ）
- IME がオンのままだと Space が全角スペース（キーの種類なし・文字だけ）で届き選択できなかったので、全角スペース・全角の a / q も文字から読み替える

## 保存データ
`Data/CliCommands.json`（定型コマンド）・`Data/CliSettings.json`（最終ディレクトリ・履歴 20 件）。どちらもローカル専用で、読み込みに失敗したときは上書きせず InfoBar で知らせる。詳細は [storage.md](storage.md)。

## 後回し
- 既定シェルの差し替え：設定ページの項目ではなく、CLI補助の機能として組み込む。目的は Kiro でも動かすこと、できれば WSL でも動かすこと（パスの問題などがある）
- クリップボード転送
