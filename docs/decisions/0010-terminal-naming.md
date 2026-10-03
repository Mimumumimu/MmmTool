# 0010: 「ターミナル」と「シェル」の呼び分け

## 背景
「Terminal」が、同じ CLI補助の中で 4 つの意味に使われ、JSON の `"terminal"`（定型コマンドのタブ）と `"focus": "terminal"`（ペイン）が紛らわしかった。

## 決定
- **ターミナル**: 中央のペイン、およびその実体（ConPTY・xterm.js）だけを指す。名前空間 `MmmSdk.WinUI.Components.Terminal`（SDK）、`TerminalControl`、`ITerminalSession`、`FocusTarget.Terminal`（`"focus": "terminal"`）はこの意味
- **シェル**: 定型コマンドの左のタブのうち、シェルで打つコマンド（AI エージェント起動前）。`CommandCategory.Shell`、`CliCommandSet.Shell`、JSON の `"shell"` と `"switchTo": "shell"`、画面の表記「シェル」
- **AI セッション**: もう一方のタブ（`CommandCategory.Session`、`"session"`）。変更なし

## 影響
- 定型コマンドの JSON は `terminal` → `shell` に変わる。古いキー（`terminal`）は読まない（個人利用で、既定から作り直せるため、互換のための読み替えは持たない）。作り直すには、アプリを閉じて `Data/CliCommands.json` を削除する

## 理由
タブとペインで同じ語を使うと、`switchTo` と `focus` の値が読み間違いやすい。ペインは「ターミナル」、コマンドを打つ相手の種類は「シェル」と「AI セッション」で呼び分ければ、値の意味が名前から分かる。
