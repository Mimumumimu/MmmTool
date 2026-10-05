"""会話履歴の削除スクリプトの共通部品 (WSL・Linux 版): 矢印キーで選ぶ画面。

SessionMenu.ps1 (Windows 版)と同じ操作にしている。直すときは両方を直す。
Remove-ClaudeSession.py と Remove-KiroSession.py が使う。
"""

import os
import re
import select
import shutil
import sys
import termios
import tty
import unicodedata

ESC = "\x1b"
CYAN = f"{ESC}[36m"
GRAY = f"{ESC}[90m"
YELLOW = f"{ESC}[33m"
GREEN = f"{ESC}[32m"
RESET = f"{ESC}[0m"
CLEAR_LINE = f"{ESC}[K"


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
