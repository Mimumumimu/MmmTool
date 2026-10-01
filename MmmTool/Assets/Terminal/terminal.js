// xterm.js とホスト（C# 側 WebView2）の橋渡し。
// ホスト → JS : { type: "output", data } / { type: "focus" }
// JS → ホスト : { type: "ready", cols, rows } / { type: "input", data } / { type: "resize", cols, rows }
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
            navigator.clipboard.writeText(term.getSelection());
            term.clearSelection();
            return false;
        }
        if (e.key === "v") {
            return false;
        }
        return true;
    });

    term.onData(data =>host.postMessage({ type: "input", data }));
    term.onResize(({ cols, rows }) => host.postMessage({ type: "resize", cols, rows }));

    new ResizeObserver(() => fit.fit()).observe(document.body);

    host.addEventListener("message", e => {
        const message = e.data;
        switch (message.type) {
            case "output":
                term.write(message.data);
                break;
            case "focus":
                term.focus();
                break;
        }
    });

    host.postMessage({ type: "ready", cols: term.cols, rows: term.rows });
})();
