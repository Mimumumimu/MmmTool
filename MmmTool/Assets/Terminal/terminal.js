// xterm.js とホスト（C# 側 WebView2）の橋渡し。
// ホスト → JS : { type: "output", data } / { type: "focus" } / { type: "submit", data }
// JS → ホスト : { type: "ready", cols, rows } / { type: "input", data } / { type: "resize", cols, rows } / { type: "written", length }
(() => {
    "use strict";

    const host = window.chrome.webview;

    const term = new Terminal({
        fontFamily: "'Cascadia Mono', Consolas, 'BIZ UDGothic', 'MS Gothic', monospace",
        fontSize: 14,
        cursorBlink: true,
        scrollback: 10000,
        theme: {
            background: "#0c0c0c",
            foreground: "#cccccc",
        },
    });

    const fit = new FitAddon.FitAddon();
    term.loadAddon(fit);
    term.open(document.getElementById("terminal"));
    fit.fit();

    // Ctrl+C: 選択中ならコピー、選択が無ければ通常どおり中断（^C）を送る
    // Ctrl+V: 端末へ ^V を送らず、ブラウザの貼り付け（xterm の paste 処理）に任せる
    term.attachCustomKeyEventHandler(e => {
        if (e.type !== "keydown" || !e.ctrlKey || e.shiftKey || e.altKey) {
            return true;
        }
        if (e.key === "c" && term.hasSelection()) {
            // 書き込めたときだけ選択を解除する（拒否されたら選択を残して、コピーされていないと分かるようにする）
            navigator.clipboard.writeText(term.getSelection())
                .then(() => term.clearSelection())
                .catch(error => console.error("クリップボードへ書き込めませんでした", error));
            return false;
        }
        if (e.key === "v") {
            return false;
        }
        return true;
    });

    term.onData(data =>host.postMessage({ type: "input", data }));
    term.onResize(({ cols, rows }) => host.postMessage({ type: "resize", cols, rows }));

    // サイズ変更の通知は 1 フレームに 1 回にまとめる（ドラッグ中に fit() を連打しない。onResize は、列・行が実際に変わったときだけ ConPTY へ伝える）
    let fitScheduled = false;
    new ResizeObserver(() => {
        if (fitScheduled) {
            return;
        }
        fitScheduled = true;
        requestAnimationFrame(() => {
            fitScheduled = false;
            fit.fit();
        });
    }).observe(document.body);

    // 最後にシェルの出力を受け取った時刻（送信時に、CLI の処理が落ち着いたかの判断に使う）
    let lastOutputAt = 0;

    // 送信の待ち方: 貼り付け後、CLI が一度画面を書き換え、その出力が QUIET_MS 途切れたら Enter（最低 MIN_WAIT_MS、最長 MAX_WAIT_MS）。
    // 長い貼り付けを CLI がまとめて「[Pasted text …]」に置き換える場合、置き換えの描画より先に Enter が届くと
    // 確定されないため、貼り付け後の出力が来る前は待ち続ける
    const MIN_WAIT_MS = 150;
    const QUIET_MS = 200;
    const MAX_WAIT_MS = 2000;
    const POLL_MS = 30;

    // テキストを貼り付けとして入力（CLI が対応していればブラケットペースト）し、Enter で確定する。
    // CLI は貼り付けの処理中に届いた Enter を本文の一部（改行）として扱うことがあるため、
    // 貼り付けへの反応（画面の書き換え）が落ち着くのを待ってから Enter を送る
    const submit = text => {
        const pastedAt = performance.now();
        term.paste(text);

        const timer = setInterval(() => {
            const now = performance.now();
            const elapsed = now - pastedAt;
            const quiet = lastOutputAt > pastedAt && now - lastOutputAt >= QUIET_MS;
            if ((elapsed >= MIN_WAIT_MS && quiet) || elapsed >= MAX_WAIT_MS) {
                clearInterval(timer);
                host.postMessage({ type: "input", data: "\r" });
            }
        }, POLL_MS);
    };

    host.addEventListener("message", e => {
        const message = e.data;
        switch (message.type) {
            case "output":
                lastOutputAt = performance.now();
                // 描画が終わったら、その文字数をホストへ返す（ホストは未返却が多いと出力を送らず待つ）
                term.write(message.data, () => host.postMessage({ type: "written", length: message.data.length }));
                break;
            case "focus":
                term.focus();
                break;
            case "submit":
                submit(message.data);
                break;
        }
    });

    host.postMessage({ type: "ready", cols: term.cols, rows: term.rows });
})();
