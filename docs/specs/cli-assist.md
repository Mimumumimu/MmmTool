# CLI補助

AI のコマンドラインツール（Claude Code・Kiro など）を使うときの補助画面。ターミナルの下に送信欄を置き、定型コマンドの送信・送信履歴・添付を助ける。`Features/CliAssist/`（中は `Terminal/`・`WorkingDirectory/`・`Attachments/` に分けている。Core 側は `MmmTool.Core/CliAssist/`）。

## 画面
- 3 領域のレイアウト：左に定型コマンドのツリー、中央にターミナル、下に送信欄
- ターミナルは ConPTY + xterm.js（WebView2）。入出力・リサイズに対応し、シェルが終了したあとは何かキーを押すと再起動する
  - 再起動（`ITerminalSession.RestartAsync`）では、古いシェルの終了待ち（最大で数秒）を UI スレッドの外で行う。終了待ちの間に押されたキーは捨てる（二重に起動し直さない）。アプリの終了時の `Dispose` だけは、シェルを確実に終わらせるため、同期で待つ
  - 出力は、シェルの出力・終了メッセージ・起動失敗のメッセージを、すべて同じバッファ経由で送る（順序が入れ替わらない）。xterm.js が描画し終えるたびに文字数を返し（`written`）、未返却が 1M 文字を超えたら、返ってくるまで送らずにためておく（xterm.js の書き込み待ちがあふれて出力が捨てられるのを防ぐ）。ためる側（C# のバッファ）の大きさには上限を設けていない
  - WebView2 を初期化できなかったとき（ランタイムが無い・起動できない。`COMException`）は、ターミナルの場所に理由を文字で出す（他の機能は使える）。制約: ランタイムが無い PC で実際にどの例外が出るかは、まだ実機で確かめていない。COMException 以外が出たときは、未処理例外の受け皿（ログ・ダイアログ・終了）が受ける。確かめられたら、受ける例外を直す
  - シェルは pwsh.exe が PATH にあればそれ、無ければ powershell.exe（`DefaultShell`。Core）
  - 描画は `Features/CliAssist/Terminal/TerminalControl`（WebView2 で `Assets/Terminal/` の xterm.js を仮想ホスト経由で表示）。C# ↔ JS は JSON メッセージ（`terminal.js` の冒頭に一覧）
  - `PseudoConsoleSession`（ConPTY は SDK の `PseudoConsole`。ここでは出力の読み取り・終了の通知・入力の確定）を `ITerminalSession` として DI に Transient で登録し、Host の破棄時に Dispose する
- 作業ディレクトリ変更ダイアログ（最近使ったフォルダの履歴・フォルダ選択・存在確認）
  - 存在確認（`Directory.Exists`）は、ネットワークパスで止まることがあるため、UI スレッドの外で行う。入力欄の変更は少し待ってから（デバウンス）確認する。起動時の作業ディレクトリ（`CliSettingsService.StartDirectory`）も、読み込みの中でバックグラウンドで確認し、存在するときだけ使う

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
- サムネイルは `ThumbnailImage.FromFile`（ファイルを開いたままにしない＝削除できなくならないよう、中身をメモリに読み込んでから表示する）

## 補助スクリプト：会話履歴の削除
- `Assets/Tools/Remove-ClaudeSession.ps1`：Claude Code の会話履歴を矢印キーで選んでごみ箱へ送る（`~/.claude/projects` 配下の `.jsonl` が対象）
- 出力フォルダへコピーされ、定型コマンド「会話履歴の削除」（シェル › Claude Code）から `<シェル> -NoProfile -File "{AppDir}\Assets\Tools\..."` で呼ぶ
- ファイルは日本語を含むので BOM 付き UTF-8（PowerShell 5.1 の文字化け対策）
- 矢印キーの選択画面は実ターミナルが必要（入力がリダイレクトされているときは番号入力に切り替わる）
- 操作：↑↓ 移動 / Space 選択 / Tab 選択して下へ（Shift+Tab は上へ） / a 全選択 / Enter 決定（未選択ならカーソル行） / Esc・Backspace 戻る / q・Ctrl+C 終了。削除後はプロジェクト選択へ戻る
- IME がオンのままだと Space が全角スペース（キーの種類なし・文字だけ）で届き選択できなかったので、全角スペース・全角の a / q も文字から読み替える

## 保存データ
`Data/CliCommands.json`（定型コマンド）・`Data/CliSettings.json`（最終ディレクトリ・履歴 20 件）。どちらもローカル専用で、読み込みに失敗したときは上書きせず InfoBar で知らせる。詳細は [storage.md](storage.md)。

## 後回し
- 既定シェルの差し替え：設定ページの項目ではなく、CLI補助の機能として組み込む。目的は Kiro でも動かすこと、できれば WSL でも動かすこと（パスの問題などがある）
- クリップボード転送
