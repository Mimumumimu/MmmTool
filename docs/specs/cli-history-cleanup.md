# CLI補助: 会話履歴の削除 (Claude Code・Kiro)

CLI補助の定型コマンド「会話履歴の削除」から呼ぶ補助スクリプト。画面・定型コマンドは [cli-assist.md](cli-assist.md)。ツールごとにスクリプトが 1 組 (`.ps1` と `.py`)あり、矢印キーで選ぶ画面は共通の部品を使う。
- 共通の部品: `Assets/Tools/SessionMenu.ps1`(Windows)・`Assets/Tools/session_menu.py`(WSL)。矢印キーで選ぶ画面 (キーの読み取り・一覧の表示・番号入力への切り替え)。2 つは同じ操作にしている (直すときは両方を直す)。各スクリプトが読み込む (`.ps1` は dot-source、`.py` は `import`。Python は出力フォルダに `__pycache__` を作らない)。画面の流れ (プロジェクト選択 → セッション選択 → 確認・削除)は、データの持ち方がツールごとに違うので、各スクリプトに書く
- `Assets/Tools/Remove-ClaudeSession.ps1`(Windows)・`Assets/Tools/Remove-ClaudeSession.py`(WSL)：Claude Code の会話履歴を矢印キーで選んでごみ箱へ送る (`~/.claude/projects` 配下の `.jsonl` が対象)。2 つは同じ流れ・同じ操作にしている (直すときは両方を直す)
- 出力フォルダ (`Assets\Tools\`。ソースは `src/Plugins/MmmTool.CliAssist/Tools/`)へコピーされ、定型コマンド「会話履歴の削除」 (シェル › Claude Code)から呼ぶ。Windows は `<シェル> -NoProfile -File "{AppDir}\Assets\Tools\Remove-ClaudeSession.ps1"`、WSL は `python3 "{AppDir}/Assets/Tools/Remove-ClaudeSession.py"`(`{AppDir}` は `/mnt/d/...` に展開される)
- WSL 版 (Python 3)
  - WSL には Windows のごみ箱が無く、PowerShell 版を WSL の履歴 (`\\wsl.localhost\...`)に向けると、ネットワークのパスなので戻せない形で消えてしまうため、別に作った。Linux の標準のごみ箱 (freedesktop.org の Trash。`~/.local/share/Trash` の `files/` と `info/*.trashinfo`)へ移す。戻すときは、`files/` から元の場所 (`.trashinfo` の `Path`)へ移す (trash-cli の `trash-restore`・`gio trash --restore` でも戻せる)
  - Python 3 は Ubuntu に標準で入っている。キー入力は端末を raw モードにして読む (`termios`)。Ctrl+C もキーとして受け取る。Esc キー単独か、矢印などの続きかは、50ms 待って見分ける
  - オプションは `--all` / `--what-if` / `--root`(PowerShell 版の `-All` / `-WhatIf` / `-Root`)
  - 改行は LF にする (`.gitattributes` の `*.py text eol=lf`。Windows で取り出しても CRLF にしない)
- PowerShell 版のファイルは日本語を含むので BOM 付き UTF-8 (PowerShell 5.1 の文字化け対策)。Python 版は BOM なしの UTF-8
- タイトルの取得は、`.jsonl` を 1 行ずつ文字列で探して、最後の `custom-title` 行 (名前を付けたセッション名。`/resume` が優先して出す)を使い、無ければ最後の `ai-title` 行を使う (`Select-String` のパイプラインを通さない)。どちらも無ければ最初のユーザー発言、それも無ければ「(発言なし)」。`/clear` だけのセッションは「(発言なし)」になる
- 実行中のセッションかどうかは調べない。実行中かを確実に判定する方法が無いため (Claude Code はファイルが開かれたままかで判定できず、Kiro は `<id>.lock` の有無が実行中を表すとは限らない)。Claude Code は、ごみ箱への移動なので間違えても戻せる。Kiro は戻せないので、確認の文言に「戻せません」と出し、削除のあとに一覧を取り直して消えたかを確かめる。説明文では、開いているセッションは先に閉じるよう求める
- Kiro 版 (`Assets/Tools/Remove-KiroSession.ps1`(Windows)・`Assets/Tools/Remove-KiroSession.py`(WSL))：Kiro CLI の会話履歴を、作業フォルダ (cwd)ごとにまとめて選んで削除する。流れ・操作・オプション (`-All` / `-WhatIf`。Python は `--all` / `--what-if`)は Claude Code 版と同じ。呼び方も同じ (`Remove-KiroSession` の `.ps1` / `.py`)
  - 一覧は `kiro-cli chat --list-sessions --all-cwds --format json` で取る。出力は、作業フォルダごとの配列で、各要素が `cwd` と `sessions` (`sessionId` / `updatedAt` / `messageCount` / `title`)を持つ。WSL 版は Windows の Kiro ではなく、WSL の中の `kiro-cli` を使う
  - 削除は `kiro-cli chat --delete-session <sessionId>`。Kiro の履歴は 1 つの SQLite にあり、ごみ箱へは送れないので、戻せない。確認の文言にも「戻せません」と出す。公式の削除が消し残す `<id>.history` / `<id>.lock` は、`${KIRO_HOME:-~/.kiro}/sessions/cli` から消す (ID は英数字・ハイフン・アンダースコアだけ受け付ける)
  - v3 のセッションは、削除できていても「session not found」のエラーが出ることがある (Kiro の既知の不具合)。終了コード・エラー出力は見ず、削除のあとに一覧を取り直して、消えたかを確かめる。残っていれば「削除できませんでした」と出す
  - ファイルを直接読まず、公式コマンドだけを使う (Kiro の保存の形が変わっても壊れない)。`kiro-cli` が無ければ「見つかりません」と出して終わる。応答が 60 秒を超えたら (WSL 版)終了する
  - 日時は UTC の ISO 8601 で届くので、ローカル時刻にして表示する。タイトルは 1 行にして 60 文字で切り詰め、無ければ「(タイトルなし)」
- 矢印キーの選択画面は実ターミナルが必要 (入力がリダイレクトされているときは番号入力に切り替わる)
- 操作：↑↓ 移動 / Space 選択 / Tab 選択して下へ (Shift+Tab は上へ) / a 全選択 / Enter 決定 (未選択ならカーソル行) / Esc・Backspace 戻る / q・Ctrl+C 終了。削除後は同じプロジェクトのセッション選択へ戻る (残りが 1 件も無ければプロジェクト選択へ)
- IME がオンのままだと Space が全角スペース (キーの種類なし・文字だけ)で届き選択できなかったので、全角スペース・全角の a / q も文字から読み替える
