#!/usr/bin/env python3
"""Kiro CLI の会話履歴を選んで削除する (WSL・Linux 版)。

Remove-KiroSession.ps1 (Windows 版)と同じ流れ・同じ操作にしている。直すときは両方を直す。

ターミナル内で完結する (別ウィンドウなし)。
  1. 作業フォルダの一覧から選ぶ
  2. 選んだフォルダ内のセッションを選ぶ
  3. 確認のうえ、削除 → 2 に戻る (q で終了するまで繰り返せる)

一覧の取得と削除は、kiro-cli の公式コマンド (chat --list-sessions / --delete-session)で行う。
Kiro の削除は戻せない (ごみ箱へは送らない)。

操作: ↑↓ 移動 / Space 選択・解除 / Tab 選択・解除して下へ (Shift+Tab は上へ) / a 全選択・全解除 / Enter 決定 (未選択ならカーソル行)
      Backspace Esc 1 つ前に戻る / q 終了
入力がリダイレクトされている場合は番号入力 (例: 1,3,5-7 / a / b=戻る / q=終了)になる。

--all を付けると手順 2 を省略し、選んだフォルダの全セッションを対象にする。
--what-if を付けると、削除せず対象を表示するだけ。
Kiro で開いているセッションは消さないこと (先に閉じる)。
"""

import argparse
import datetime
import json
import os
import re
import shutil
import subprocess
import sys

# 画面部品は同じフォルダの session_menu.py (出力フォルダに __pycache__ を作らない)
sys.dont_write_bytecode = True
from session_menu import GREEN, RESET, YELLOW, read_selection  # noqa: E402

KIRO_TIMEOUT_SECONDS = 60


def run_kiro(kiro_args):
    """kiro-cli を動かして標準出力を返す。エラー出力は捨てる (結果は一覧の取り直しで確かめる)"""
    result = subprocess.run(
        ["kiro-cli", *kiro_args],
        stdin=subprocess.DEVNULL, stdout=subprocess.PIPE, stderr=subprocess.DEVNULL,
        encoding="utf-8", errors="replace", timeout=KIRO_TIMEOUT_SECONDS)
    return result.stdout


def parse_time(value):
    """更新日時 (ISO 8601 の文字列)をローカル時刻にする"""
    text = str(value).replace("Z", "+00:00")
    # 小数点以下は 6 桁まで (fromisoformat の制限)
    text = re.sub(r"(\.\d{6})\d+", r"\1", text)
    parsed = datetime.datetime.fromisoformat(text)
    if parsed.tzinfo is None:
        parsed = parsed.replace(tzinfo=datetime.timezone.utc)
    return parsed.astimezone()


def format_title(title):
    """タイトルを 1 行にして 60 文字で切り詰める"""
    text = re.sub(r"\s+", " ", str(title or "")).strip()
    if not text:
        return "(タイトルなし)"
    return text[:60] + "…" if len(text) > 60 else text


def get_groups():
    """全作業フォルダのセッションを、フォルダごとにまとめて新しい順に返す"""
    text = run_kiro(["chat", "--list-sessions", "--all-cwds", "--format", "json"])
    match = re.search(r"[\[{]", text)
    if not match:
        return []
    try:
        data = json.loads(text[match.start():])
    except ValueError:
        raise SystemExit(f"セッション一覧を読み取れません。kiro-cli の出力: {text}")
    if isinstance(data, dict):
        data = [data]

    groups = []
    for entry in data:
        cwd = entry.get("cwd") or "(cwd 不明)"
        sessions = [
            {
                "id": str(s["sessionId"]),
                "updated": parse_time(s.get("updatedAt")),
                "title": format_title(s.get("title")),
                "cwd": cwd,
            }
            for s in entry.get("sessions") or [] if s.get("sessionId")
        ]
        if sessions:
            sessions.sort(key=lambda s: s["updated"], reverse=True)
            groups.append({"name": cwd, "count": len(sessions), "last": sessions[0]["updated"], "sessions": sessions})
    return sorted(groups, key=lambda g: g["last"], reverse=True)


def get_sessions(groups):
    sessions = [s for g in groups for s in g["sessions"]]
    return sorted(sessions, key=lambda s: s["updated"], reverse=True)


def format_time(value):
    return value.strftime("%Y-%m-%d %H:%M")


def session_directory():
    """公式の削除コマンドが消し残す <id>.history / <id>.lock を探すフォルダ"""
    kiro_home = os.environ.get("KIRO_HOME") or os.path.join(os.path.expanduser("~"), ".kiro")
    return os.path.join(kiro_home, "sessions", "cli")


def remove_session(session_id):
    # ID はファイル名に使うので、英数字・ハイフン・アンダースコアだけを受け付ける
    if not re.fullmatch(r"[0-9A-Za-z_-]+", session_id):
        return
    # v3 のセッションは、削除できていても「見つからない」というエラーが出るので、結果は呼び出し側が一覧の取り直しで確かめる
    run_kiro(["chat", "--delete-session", session_id])
    for ext in ("history", "lock"):
        path = os.path.join(session_directory(), f"{session_id}.{ext}")
        if os.path.isfile(path):
            os.remove(path)


def main():
    parser = argparse.ArgumentParser(description="Kiro CLI の会話履歴を選んで削除する")
    parser.add_argument("--all", action="store_true", help="選んだフォルダの全セッションを対象にする")
    parser.add_argument("--what-if", action="store_true", help="削除せず対象を表示するだけ")
    args = parser.parse_args()

    if shutil.which("kiro-cli") is None:
        print("kiro-cli が見つかりません。")
        return

    # 画面の流れ: 1 作業フォルダ選択 → 2 セッション選択 → 3 確認・削除 → 2 へ
    step = 1
    picked_groups = []
    sessions = []
    targets = []

    while True:
        if step == 1:
            groups = get_groups()
            if not groups:
                print("履歴がありません。")
                return

            action, picked = read_selection(
                groups,
                lambda g: f"{format_time(g['last'])} | {g['count']:3} 件 | {g['name']}",
                "作業フォルダを選択", "終了")
            if action != "ok":
                print("終了しました。")
                return

            picked_groups = picked
            sessions = get_sessions(picked_groups)
            step = 2
        elif step == 2:
            if args.all:
                targets = sessions
                step = 3
                continue

            multi = len(picked_groups) > 1
            action, picked = read_selection(
                sessions,
                lambda s: f"{format_time(s['updated'])} | {s['title']}" + (f"  [{s['cwd']}]" if multi else ""),
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
            print(f"{len(targets)} 件を削除します (戻せません)。")
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
                    remove_session(target["id"])

                # 一覧を取り直して、消えたかを確かめる。同じフォルダのセッション選択へ戻る (残りが 1 件も無ければフォルダ選択へ)
                groups = get_groups()
                remain_ids = {s["id"] for g in groups for s in g["sessions"]}
                failed = [t for t in targets if t["id"] in remain_ids]
                if failed:
                    print(f"{YELLOW}{len(failed)} 件を削除できませんでした (Kiro で開いていないか確認してください)。{RESET}")
                    for target in failed:
                        print(f"  {format_time(target['updated'])} | {target['title']}")
                if len(failed) < len(targets):
                    print(f"{GREEN}完了しました。{RESET}")

                names = {g["name"] for g in picked_groups}
                picked_groups = [g for g in groups if g["name"] in names]
                sessions = get_sessions(picked_groups) if picked_groups else []
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
    except subprocess.TimeoutExpired:
        print(f"kiro-cli が {KIRO_TIMEOUT_SECONDS} 秒以内に応答しませんでした。")
