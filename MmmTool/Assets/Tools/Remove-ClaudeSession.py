#!/usr/bin/env python3
"""Claude Code の会話履歴 (~/.claude/projects 配下の .jsonl)を選んでごみ箱へ移動する (WSL・Linux 版)。

Remove-ClaudeSession.ps1 (Windows 版)と同じ流れ・同じ操作にしている。直すときは両方を直す。

ターミナル内で完結する (別ウィンドウなし)。
  1. プロジェクトの一覧から選ぶ
  2. 選んだプロジェクト内のセッションを選ぶ
  3. 確認のうえ、ごみ箱へ移動 → 2 に戻る (q で終了するまで繰り返せる)

操作: ↑↓ 移動 / Space 選択・解除 / Tab 選択・解除して下へ (Shift+Tab は上へ) / a 全選択・全解除 / Enter 決定 (未選択ならカーソル行)
      Backspace Esc 1 つ前に戻る / q 終了
入力がリダイレクトされている場合は番号入力 (例: 1,3,5-7 / a / b=戻る / q=終了)になる。

--all を付けると手順 2 を省略し、選んだプロジェクトの全セッションを対象にする。
--what-if を付けると、削除せず対象を表示するだけ。
Claude Code で開いているセッションは消さないこと (先に閉じる)。

ごみ箱は、Linux のデスクトップの決まり (freedesktop.org の Trash)の形 (~/.local/share/Trash の files/ と info/)。
WSL には Windows のごみ箱が無いので、こちらへ移す。戻すときは files/ から元の場所 (info/ の .trashinfo の Path)へ移す
(trash-cli の trash-restore・gio trash --restore でも戻せる)。
"""

import argparse
import datetime
import json
import os
import re
import select
import shutil
import sys
import termios
import tty
import unicodedata
import urllib.parse

ESC = "\x1b"
CYAN = f"{ESC}[36m"
GRAY = f"{ESC}[90m"
YELLOW = f"{ESC}[33m"
GREEN = f"{ESC}[32m"
RESET = f"{ESC}[0m"
CLEAR_LINE = f"{ESC}[K"


def first_user_message(path):
    """最初のユーザー発言 (先頭 60 行だけ読む)"""
    with open(path, encoding="utf-8", errors="replace") as file:
        for index, line in enumerate(file):
            if index >= 60:
                break
            try:
                obj = json.loads(line)
            except ValueError:
                continue
            if not isinstance(obj, dict) or obj.get("type") != "user" or obj.get("isMeta"):
                continue
            content = (obj.get("message") or {}).get("content")
            if isinstance(content, str):
                text = content
            elif isinstance(content, list):
                text = " ".join(part.get("text", "") for part in content if isinstance(part, dict) and part.get("type") == "text")
            else:
                text = ""
            if not text or text.lstrip().startswith("<"):
                continue
            return text
    return "(発言なし)"


def session_title(path):
    """/resume に出るタイトル (ai-title 行の最後のもの)。無ければ最初のユーザー発言"""
    title = None
    hit_line = None
    with open(path, encoding="utf-8", errors="replace") as file:
        for line in file:
            if '"type":"ai-title"' in line:
                hit_line = line
    if hit_line:
        try:
            title = json.loads(hit_line).get("aiTitle")
        except (ValueError, AttributeError):
            title = None
    if not title:
        title = first_user_message(path)
    title = re.sub(r"\s+", " ", title).strip()
    if len(title) > 60:
        title = title[:60] + "…"
    return title


def cell_width(text):
    """全角を 2 桁として表示幅を数える"""
    return sum(2 if unicodedata.east_asian_width(c) in ("W", "F") else 1 for c in text)


def limit_cell_width(text, max_width):
    """表示幅が max_width に収まるように切り詰める"""
    if cell_width(text) <= max_width:
        return text
    result = []
    width = 0
    for c in text:
        cw = cell_width(c)
        if width + cw > max_width - 1:
            break
        result.append(c)
        width += cw
    return "".join(result) + "…"


class KeyReader:
    """端末を raw モードにして、キーを 1 つずつ読む"""

    def __init__(self):
        self._fd = sys.stdin.fileno()
        self._old = None
        self._buffer = ""
        self._decoder = None

    def __enter__(self):
        import codecs
        self._old = termios.tcgetattr(self._fd)
        # Ctrl+C もシグナルにせず、キーとして受け取って q と同じ扱いにする
        tty.setraw(self._fd)
        self._decoder = codecs.getincrementaldecoder("utf-8")("replace")
        return self

    def __exit__(self, *args):
        termios.tcsetattr(self._fd, termios.TCSADRAIN, self._old)

    def _fill(self, timeout=None):
        """入力を読み足す。timeout 秒待っても来なければ False"""
        ready, _, _ = select.select([self._fd], [], [], timeout)
        if not ready:
            return False
        self._buffer += self._decoder.decode(os.read(self._fd, 1024))
        return True

    def read(self):
        """キーを 1 つ読んで、名前 (UpArrow・Spacebar・A など。PowerShell 版のキー名に合わせる)を返す"""
        while not self._buffer:
            self._fill()
        if self._buffer[0] == ESC:
            # Esc キーだけか、矢印などの続きがあるかを、少し待って見分ける
            if len(self._buffer) == 1:
                self._fill(0.05)
            sequences = {
                f"{ESC}[A": "UpArrow", f"{ESC}OA": "UpArrow",
                f"{ESC}[B": "DownArrow", f"{ESC}OB": "DownArrow",
                f"{ESC}[5~": "PageUp", f"{ESC}[6~": "PageDown",
                f"{ESC}[H": "Home", f"{ESC}OH": "Home", f"{ESC}[1~": "Home",
                f"{ESC}[F": "End", f"{ESC}OF": "End", f"{ESC}[4~": "End",
                f"{ESC}[Z": "ShiftTab",
            }
            for sequence, name in sequences.items():
                if self._buffer.startswith(sequence):
                    self._buffer = self._buffer[len(sequence):]
                    return name
            if len(self._buffer) == 1:
                self._buffer = ""
                return "Escape"
            # 知らない続き (左右の矢印など)は、まとめて捨てる
            match = re.match(r"\x1b(\[[0-9;]*[ -/]*[@-~]|O.|.)", self._buffer)
            self._buffer = self._buffer[match.end() if match else 1:]
            return ""

        c = self._buffer[0]
        self._buffer = self._buffer[1:]
        # IME がオンだと Space・a・q が全角文字で届くので、文字から読み替える
        names = {
            "\x03": "CtrlC", "\r": "Enter", "\n": "Enter", "\t": "Tab", "\x7f": "Backspace", "\x08": "Backspace",
            " ": "Spacebar", "　": "Spacebar",
            "a": "A", "A": "A", "ａ": "A", "Ａ": "A",
            "q": "Q", "Q": "Q", "ｑ": "Q", "Ｑ": "Q",
        }
        return names.get(c, "")


def read_selection_menu(items, label, prompt, back_label):
    """矢印キー・スペースで選ぶ。戻り値は (action, items)。action は ok / back / quit"""
    selected = set()
    cursor = 0
    offset = 0
    height = shutil.get_terminal_size().lines
    page_size = max(5, height - 5)
    rows = min(len(items), page_size)
    labels = [label(item) for item in items]

    out = sys.stdout
    print(prompt)
    print(f"{GRAY}↑↓:移動  Space:選択/解除  Tab:選択して下へ  a:全選択/全解除  Enter:決定(未選択ならカーソル行)  Esc:{back_label}  q/Ctrl+C:終了{RESET}")
    # 一覧と件数の行の場所を先に空けて (下端ならスクロールして)から、先頭へ戻ってその位置を覚える
    out.write("\n" * (rows + 1))
    out.write(f"{ESC}[{rows + 1}A{ESC}7{ESC}[?25l")
    out.flush()

    try:
        with KeyReader() as keys:
            while True:
                if cursor < offset:
                    offset = cursor
                if cursor >= offset + rows:
                    offset = cursor - rows + 1
                width = shutil.get_terminal_size().columns - 1
                out.write(f"{ESC}8")
                for r in range(rows):
                    i = offset + r
                    mark = "[x]" if i in selected else "[ ]"
                    arrow = ">" if i == cursor else " "
                    line = limit_cell_width(f"{arrow} {mark} {labels[i]}", width)
                    out.write(f"\r{CYAN if i == cursor else ''}{line}{RESET}{CLEAR_LINE}\r\n")
                out.write(f"\r{GRAY}{len(selected)} / {len(items)} 件選択中{RESET}{CLEAR_LINE}")
                out.flush()

                key = keys.read()
                if key == "CtrlC" or key == "Q":
                    return "quit", []
                if key == "UpArrow":
                    cursor = max(0, cursor - 1)
                elif key == "DownArrow":
                    cursor = min(len(items) - 1, cursor + 1)
                elif key == "PageUp":
                    cursor = max(0, cursor - rows)
                elif key == "PageDown":
                    cursor = min(len(items) - 1, cursor + rows)
                elif key == "Home":
                    cursor = 0
                elif key == "End":
                    cursor = len(items) - 1
                elif key == "Spacebar":
                    selected ^= {cursor}
                elif key in ("Tab", "ShiftTab"):
                    # 選択/解除して隣へ進む (Tab=下、Shift+Tab=上)。連続した行を続けて選びやすい
                    selected ^= {cursor}
                    cursor = max(0, cursor - 1) if key == "ShiftTab" else min(len(items) - 1, cursor + 1)
                elif key == "A":
                    selected = set() if len(selected) == len(items) else set(range(len(items)))
                elif key == "Enter":
                    # 何も選んでいなければ、カーソル位置の 1 件を対象にする
                    picked = sorted(selected) if selected else [cursor]
                    return "ok", [items[i] for i in picked]
                elif key in ("Backspace", "Escape"):
                    return "back", []
    finally:
        # 件数の行の次の行へ移る (下端ならスクロールする)
        out.write(f"{ESC}8{ESC}[{rows}B\r\n{ESC}[?25h")
        out.flush()


def read_selection_text(items, label, prompt, back_label):
    """番号入力で選ぶ (キー操作できないとき用)"""
    print(prompt)
    for i, item in enumerate(items):
        print(f"{i + 1:3}. {label(item)}")
    while True:
        try:
            text = input(f"番号 (例: 1,3,5-7 / a=すべて / b={back_label} / q=終了): ")
        except EOFError:
            return "quit", []
        text = text.strip()
        if re.fullmatch(r"[qQ]", text):
            return "quit", []
        if text == "" or re.fullmatch(r"[bB]", text):
            return "back", []
        if re.fullmatch(r"[aA]", text):
            return "ok", list(items)
        indexes = set()
        ok = True
        for part in [p for p in re.split(r"[,\s]+", text) if p]:
            range_match = re.fullmatch(r"(\d+)-(\d+)", part)
            single_match = re.fullmatch(r"(\d+)", part)
            if range_match:
                a, b = int(range_match.group(1)), int(range_match.group(2))
            elif single_match:
                a = b = int(single_match.group(1))
            else:
                ok = False
                break
            if a < 1 or b > len(items) or a > b:
                ok = False
                break
            indexes.update(range(a, b + 1))
        if ok and indexes:
            return "ok", [items[i - 1] for i in sorted(indexes)]
        print(f"{YELLOW}入力が正しくありません。{RESET}")


def read_selection(items, label, prompt, back_label):
    if not sys.stdin.isatty():
        return read_selection_text(items, label, prompt, back_label)
    return read_selection_menu(items, label, prompt, back_label)


def jsonl_files(directory):
    return [entry.path for entry in os.scandir(directory) if entry.is_file() and entry.name.endswith(".jsonl")]


def get_projects(root):
    projects = []
    for entry in os.scandir(root):
        if not entry.is_dir():
            continue
        files = jsonl_files(entry.path)
        if files:
            projects.append({
                "name": entry.name,
                "count": len(files),
                "last": max(os.path.getmtime(f) for f in files),
                "path": entry.path,
            })
    return sorted(projects, key=lambda p: p["last"], reverse=True)


def get_sessions(projects):
    sessions = []
    for project in projects:
        for file in jsonl_files(project["path"]):
            sessions.append({
                "project": project["name"],
                "updated": os.path.getmtime(file),
                "title": session_title(file),
                "file": file,
            })
    return sorted(sessions, key=lambda s: s["updated"], reverse=True)


def format_time(timestamp):
    return datetime.datetime.fromtimestamp(timestamp).strftime("%Y-%m-%d %H:%M")


def trash_directory():
    data_home = os.environ.get("XDG_DATA_HOME") or os.path.join(os.path.expanduser("~"), ".local", "share")
    return os.path.join(data_home, "Trash")


def move_to_trash(path):
    """ごみ箱 (~/.local/share/Trash)へ移動する。同じ名前があれば、名前に番号を付ける"""
    trash = trash_directory()
    files_dir = os.path.join(trash, "files")
    info_dir = os.path.join(trash, "info")
    os.makedirs(files_dir, exist_ok=True)
    os.makedirs(info_dir, exist_ok=True)

    base = os.path.basename(path)
    name = base
    number = 2
    while True:
        try:
            # 戻すときの情報を先に書く (同時に同じ名前を作らないよう、新規作成でだけ開く)
            info = os.open(os.path.join(info_dir, name + ".trashinfo"), os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600)
            break
        except FileExistsError:
            name = f"{base}.{number}"
            number += 1
    with os.fdopen(info, "w", encoding="utf-8") as file:
        file.write("[Trash Info]\n")
        file.write(f"Path={urllib.parse.quote(os.path.abspath(path))}\n")
        file.write(f"DeletionDate={datetime.datetime.now().strftime('%Y-%m-%dT%H:%M:%S')}\n")
    shutil.move(path, os.path.join(files_dir, name))


def remove_session(file):
    move_to_trash(file)
    # セッションに付随するフォルダ (<セッションID>/)があれば一緒に移動
    sub = os.path.splitext(file)[0]
    if os.path.isdir(sub):
        move_to_trash(sub)


def main():
    parser = argparse.ArgumentParser(description="Claude Code の会話履歴を選んでごみ箱へ移動する")
    parser.add_argument("--all", action="store_true", help="選んだプロジェクトの全セッションを対象にする")
    parser.add_argument("--what-if", action="store_true", help="削除せず対象を表示するだけ")
    parser.add_argument("--root", default=os.path.join(os.path.expanduser("~"), ".claude", "projects"))
    args = parser.parse_args()

    if not os.path.isdir(args.root):
        print(f"見つかりません: {args.root}")
        return

    # 画面の流れ: 1 プロジェクト選択 → 2 セッション選択 → 3 確認・削除 → 2 へ
    step = 1
    picked_projects = []
    sessions = []
    targets = []

    while True:
        if step == 1:
            projects = get_projects(args.root)
            if not projects:
                print("履歴がありません。")
                return

            action, picked = read_selection(
                projects,
                lambda p: f"{format_time(p['last'])} | {p['count']:3} 件 | {p['name']}",
                "プロジェクトを選択", "終了")
            if action != "ok":
                print("終了しました。")
                return

            picked_projects = picked
            sessions = get_sessions(picked_projects)
            step = 2
        elif step == 2:
            if args.all:
                targets = sessions
                step = 3
                continue

            multi = len(picked_projects) > 1
            action, picked = read_selection(
                sessions,
                lambda s: f"{format_time(s['updated'])} | {s['title']}" + (f"  [{s['project']}]" if multi else ""),
                "削除するセッションを選択", "戻る")
            if action == "quit":
                print("終了しました。")
                return
            if action == "back":
                step = 1
                continue

            targets = picked
            step = 3
        else:
            print()
            print(f"{len(targets)} 件をごみ箱へ移動します。")
            for target in targets:
                print(f"  {format_time(target['updated'])} | {target['title']}")

            if args.what_if:
                print("(--what-if のため削除しません)")
                step = 1 if args.all else 2
                continue
            try:
                answer = input("実行しますか？ (y/N) ")
            except EOFError:
                answer = ""
            if re.match(r"[yY]", answer):
                for target in targets:
                    remove_session(target["file"])
                print(f"{GREEN}完了しました。{RESET}")

                # 同じプロジェクトのセッション選択へ戻る。残りが 1 件も無ければプロジェクト選択へ
                names = {p["name"] for p in picked_projects}
                picked_projects = [p for p in get_projects(args.root) if p["name"] in names]
                sessions = get_sessions(picked_projects) if picked_projects else []
                step = 2 if sessions and not args.all else 1
            else:
                print("取りやめました。")
                step = 1 if args.all else 2
            print()


if __name__ == "__main__":
    try:
        main()
    except KeyboardInterrupt:
        # 番号入力・確認の途中の Ctrl+C
        print()
        print("終了しました。")
