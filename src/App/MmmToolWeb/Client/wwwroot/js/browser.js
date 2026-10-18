// ブラウザの時計の今日の日付を、yyyy-MM-dd の形で返す。ブラウザ上の .NET は端末のタイムゾーンを知らないので、ここで決める
export function todayLocal() {
    const now = new Date();
    const month = String(now.getMonth() + 1).padStart(2, "0");
    const day = String(now.getDate()).padStart(2, "0");
    return `${now.getFullYear()}-${month}-${day}`;
}

// この端末で覚えておく値。前回の宛先など、端末ごとの使い勝手の記憶なので、ブラウザの記憶である localStorage に置く。使えないときは、覚えない
const rememberedPrefix = "mmmtoolweb.";

export function getRemembered(key) {
    try {
        return localStorage.getItem(rememberedPrefix + key);
    } catch {
        return null;
    }
}

export function setRemembered(key, value) {
    try {
        localStorage.setItem(rememberedPrefix + key, value);
    } catch {
        // 覚えられなくても、使うのに困らない
    }
}

// 最後に開いた機能の道。いまの画面は使わない。端末に残っている古い画面が呼ぶので、消さない
export function getLastFeature() {
    return getRemembered("lastFeature");
}

export function setLastFeature(route) {
    setRemembered("lastFeature", route);
}

// 入力欄のカーソルを先頭に置く。タップで置かれる位置を、置き直すため、1 拍おく
export function placeCaretAtStart(element) {
    setTimeout(() => element.setSelectionRange(0, 0), 0);
}

// アプリが裏から前面に戻ったときに、.NET の NotifyVisible を呼ぶ。dispose で購読をやめる
export function watchVisibility(dotNetRef) {
    const handler = () => {
        if (document.visibilityState === "visible") {
            dotNetRef.invokeMethodAsync("NotifyVisible");
        }
    };
    document.addEventListener("visibilitychange", handler);
    return {
        dispose() {
            document.removeEventListener("visibilitychange", handler);
        },
    };
}
