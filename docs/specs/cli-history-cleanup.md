# CLI補助: 会話履歴の削除 (Claude Code)

CLI補助の定型コマンド「会話履歴の削除」から呼ぶ補助スクリプト。画面・定型コマンドは [cli-assist.md](cli-assist.md)。

Kiro の「会話履歴の削除」は補助スクリプトを使わず、Kiro 自身のセッション一覧を開く ([cli-assist.md](cli-assist.md) の「定型コマンド」の表)。
- `Assets/Tools/Remove-ClaudeSession.ps1`(Windows)・`Assets/Tools/Remove-ClaudeSession.py`(WSL)：Claude Code の会話履歴を矢印キーで選んでごみ箱へ送る (`~/.claude/projects` 配下の `.jsonl` が対象)。2 つは同じ流れ・同じ操作にしている (直すときは両方を直す)
- 出力フォルダへコピーされ、定型コマンド「会話履歴の削除」 (シェル › Claude Code)から呼ぶ。Windows は `<シェル> -NoProfile -File "{AppDir}\Assets\Tools\Remove-ClaudeSession.ps1"`、WSL は `python3 "{AppDir}/Assets/Tools/Remove-ClaudeSession.py"`(`{AppDir}` は `/mnt/d/...` に展開される)
- WSL 版 (Python 3)
  - WSL には Windows のごみ箱が無く、PowerShell 版を WSL の履歴 (`\\wsl.localhost\...`)に向けると、ネットワークのパスなので戻せない形で消えてしまうため、別に作った。Linux の標準のごみ箱 (freedesktop.org の Trash。`~/.local/share/Trash` の `files/` と `info/*.trashinfo`)へ移す。戻すときは、`files/` から元の場所 (`.trashinfo` の `Path`)へ移す (trash-cli の `trash-restore`・`gio trash --restore` でも戻せる)
  - Python 3 は Ubuntu に標準で入っている。キー入力は端末を raw モードにして読む (`termios`)。Ctrl+C もキーとして受け取る。Esc キー単独か、矢印などの続きかは、50ms 待って見分ける
  - オプションは `--all` / `--what-if` / `--root`(PowerShell 版の `-All` / `-WhatIf` / `-Root`)
  - 改行は LF にする (`.gitattributes` の `*.py text eol=lf`。Windows で取り出しても CRLF にしない)
- PowerShell 版のファイルは日本語を含むので BOM 付き UTF-8 (PowerShell 5.1 の文字化け対策)。Python 版は BOM なしの UTF-8
- タイトルの取得は、`.jsonl` を 1 行ずつ文字列で探して最後の `ai-title` 行を使う (`Select-String` のパイプラインを通さない)
- 実行中のセッションかどうかは調べない。削除はごみ箱への移動なので、間違えても戻せるため。実行中かを確実に判定する方法 (ファイルが開かれたままか)が無いことも理由
- 矢印キーの選択画面は実ターミナルが必要 (入力がリダイレクトされているときは番号入力に切り替わる)
- 操作：↑↓ 移動 / Space 選択 / Tab 選択して下へ (Shift+Tab は上へ) / a 全選択 / Enter 決定 (未選択ならカーソル行) / Esc・Backspace 戻る / q・Ctrl+C 終了。削除後は同じプロジェクトのセッション選択へ戻る (残りが 1 件も無ければプロジェクト選択へ)
- IME がオンのままだと Space が全角スペース (キーの種類なし・文字だけ)で届き選択できなかったので、全角スペース・全角の a / q も文字から読み替える
