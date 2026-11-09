// iPhone の Safari は、触れた合図 (touchstart)を受ける仕組みが無いと、押している間の見た目 (CSS の :active)を出さないことがある。
// 何もしない受け手を 1 つ置いて、押している間の見た目を出させる。このファイルを最初に読み込んだときに 1 回だけ置く
document.addEventListener("touchstart", () => {}, { passive: true });

// ブラウザの時計の今日の日付を、yyyy-MM-dd の形で返す。ブラウザ上の .NET は端末のタイムゾーンを知らないので、ここで決める
export function todayLocal() {
    const now = new Date();
    const month = String(now.getMonth() + 1).padStart(2, "0");
    const day = String(now.getDate()).padStart(2, "0");
    return `${now.getFullYear()}-${month}-${day}`;
}

// 端末の今の時刻を HH:mm の文字で返す。リマインダーの新規の時刻の初期値に使う
export function nowTimeLocal() {
    const now = new Date();
    return `${String(now.getHours()).padStart(2, "0")}:${String(now.getMinutes()).padStart(2, "0")}`;
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

// 端末の呼び名。パスキーの登録名と、通知の宛先の登録名に使う。iPhone と iPad は「iPhone」、それ以外は「ブラウザ」
export function deviceName() {
    return /iPhone|iPad/.test(navigator.userAgent) ? "iPhone" : "ブラウザ";
}

// ホーム画面のアプリとして開いているか。パスキー (js/webauthn.js)も使う
export function isStandalone() {
    return window.navigator.standalone === true || window.matchMedia("(display-mode: standalone)").matches;
}

// iPhone か iPad で、ホーム画面に追加していないか。通知とパスキーは、ここで開くと使えない。パソコンのブラウザは、ホーム画面が無いので、false
export function needsHomeScreen() {
    return /iPhone|iPad/.test(navigator.userAgent) && !isStandalone();
}

// 通知の状態。unsupported = この環境では使えない / needs-home-screen = iPhone でホーム画面に追加していない / denied = 許可が拒否されている / ready = 登録できる
// subscribed は、このブラウザが今、宛先を持っているか。endpoint は、その宛先の URL で、持っていないときは null。画面を開いたとき、サービスワーカーも登録しておく (押したときの待ちを短くするため)
export async function getPushStatus() {
    if (needsHomeScreen()) {
        return { state: "needs-home-screen", subscribed: false, endpoint: null };
    }
    if (!("serviceWorker" in navigator) || !("PushManager" in window) || !("Notification" in window)) {
        return { state: "unsupported", subscribed: false, endpoint: null };
    }
    if (Notification.permission === "denied") {
        return { state: "denied", subscribed: false, endpoint: null };
    }
    const registration = await registerWorker();
    const subscription = await registration.pushManager.getSubscription();
    return { state: "ready", subscribed: subscription !== null, endpoint: subscription?.endpoint ?? null };
}

// 通知の許可を求めて、このブラウザの宛先を作る。押した操作の中で、最初に呼ぶ (iPhone は、押した直後でないと許可を求められない)
// 結果の status は、granted = 作れた / denied = 許可されなかった。granted のときだけ endpoint・p256dh・auth が入る
export async function subscribePush(publicKey) {
    const permission = await Notification.requestPermission();
    if (permission !== "granted") {
        return { status: "denied" };
    }
    const registration = await registerWorker();
    const key = base64UrlToBytes(publicKey);
    let subscription = await registration.pushManager.getSubscription();
    if (subscription && !sameKey(subscription.options?.applicationServerKey, key)) {
        // 送り手の鍵が替わっているので、古い宛先は使えない。作り直す
        await subscription.unsubscribe();
        subscription = null;
    }
    subscription ??= await registration.pushManager.subscribe({ userVisibleOnly: true, applicationServerKey: key });
    const json = subscription.toJSON();
    return { status: "granted", endpoint: json.endpoint, p256dh: json.keys.p256dh, auth: json.keys.auth };
}

// このブラウザの宛先を捨てる。無ければ何もしない
export async function unsubscribePush() {
    if (!("serviceWorker" in navigator)) {
        return;
    }
    const registration = await navigator.serviceWorker.getRegistration("/");
    const subscription = registration ? await registration.pushManager.getSubscription() : null;
    if (subscription) {
        await subscription.unsubscribe();
    }
}

// サービスワーカー (sw.js)を登録して、使える状態になるまで待つ
async function registerWorker() {
    await navigator.serviceWorker.register("/sw.js", { scope: "/" });
    return navigator.serviceWorker.ready;
}

// base64url の文字列を、バイト列にする。パスキー (js/webauthn.js)も使う
export function base64UrlToBytes(text) {
    const padded = text.replace(/-/g, "+").replace(/_/g, "/").padEnd(Math.ceil(text.length / 4) * 4, "=");
    return Uint8Array.from(atob(padded), (c) => c.charCodeAt(0));
}

// 宛先に結びついた送り手の鍵と、今の鍵が同じか
function sameKey(current, wanted) {
    if (!current) {
        return false;
    }
    const bytes = new Uint8Array(current);
    return bytes.length === wanted.length && bytes.every((value, index) => value === wanted[index]);
}

// 入力欄のカーソルを先頭に置く。いまの画面は使わない。端末に残っている古い画面が呼ぶので、消さない。タップで置かれる位置を、置き直すため、1 拍おく
export function placeCaretAtStart(element) {
    setTimeout(() => element.setSelectionRange(0, 0), 0);
}

// 画面がいま前面に見えているか。読んだ位置を相手に見せてよいかの判定に使う
export function isVisible() {
    return document.visibilityState === "visible";
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

// 端末の控え。メッセージの会話などを、IndexedDB に置く。サーバーが唯一の正で、控えはいつでも作り直せる。使えなくても、使うのに困らない
// js/badge.js を読み込んでおく。端末に古い index.html が残っていて、badge.js を読んでいなくても、止まらないようにする
// 読み込めたかを返す。読み込めないときは、控えと未読の機能だけが働かない
let badgeLoading = null;

function loadBadge() {
    if (typeof globalThis.openCacheDb === "function") {
        return Promise.resolve(true);
    }
    badgeLoading ??= new Promise((resolve) => {
        const script = document.createElement("script");
        script.src = "/js/badge.js";
        script.onload = () => resolve(true);
        script.onerror = () => resolve(false);
        document.head.appendChild(script);
    });
    return badgeLoading;
}

// 控えの開き方と名前は、js/badge.js の openCacheDb と同じ。サービスワーカーも同じ控えを使う
const cacheStore = "kv";

async function openCache() {
    if (!(await loadBadge())) {
        throw new Error("badge.js を読み込めない");
    }
    return globalThis.openCacheDb();
}

// 読んでいない通知は js/badge.js が持つ。種類は kind で、message など。番号は ids
// 一覧に入っている識別子を、種類:番号 の形で返す
export async function unreadGet() {
    return (await loadBadge()) ? globalThis.readUnreadKeys() : [];
}

// 画面が取り込んだ新着を数える。ids = 数えるものの番号、maxId = 取り込んだ全部の中で一番大きい番号
export async function unreadAddArrived(kind, ids, maxId) {
    if (await loadBadge()) {
        await globalThis.addArrivedUnread(kind, ids, maxId);
    }
}

// 種類の基準の番号が無いときだけ、置く
export async function unreadInit(kind, maxId) {
    if (await loadBadge()) {
        await globalThis.initUnreadHigh(kind, maxId);
    }
}

// 読んだ通知を、一覧から外す
export async function unreadRemove(kind, ids) {
    if (await loadBadge()) {
        await globalThis.removeUnread(kind, ids);
    }
}

// サーバーが数える種類 (メッセージ)の件数を置いて、アイコンの数字を直す
export async function unreadSetServer(kind, count) {
    if (await loadBadge()) {
        await globalThis.setServerUnread(kind, count);
    }
}

// 種類の通知を、全部一覧から外す
export async function unreadClear(kind) {
    if (await loadBadge()) {
        await globalThis.clearUnread(kind);
    }
}

// 控えの値を読む。無いとき、読めないときは null
export async function cacheGet(key) {
    try {
        const db = await openCache();
        return await new Promise((resolve) => {
            const request = db.transaction(cacheStore, "readonly").objectStore(cacheStore).get(key);
            request.onsuccess = () => resolve(request.result ?? null);
            request.onerror = () => resolve(null);
        });
    } catch {
        return null;
    }
}

// 控えに値を置く。置けなくても、エラーにしない
export async function cachePut(key, value) {
    try {
        const db = await openCache();
        await new Promise((resolve) => {
            const transaction = db.transaction(cacheStore, "readwrite");
            transaction.objectStore(cacheStore).put(value, key);
            transaction.oncomplete = () => resolve();
            transaction.onerror = () => resolve();
            transaction.onabort = () => resolve();
        });
    } catch {
        // 置けなくても、使うのに困らない
    }
}

// 控えを全部捨てる。ログアウトとアカウントの変更で呼ぶ。共有の端末で、前の人のメッセージが見えないようにするため
export async function cacheClear() {
    try {
        const db = await openCache();
        await new Promise((resolve) => {
            const transaction = db.transaction(cacheStore, "readwrite");
            transaction.objectStore(cacheStore).clear();
            transaction.oncomplete = () => resolve();
            transaction.onerror = () => resolve();
            transaction.onabort = () => resolve();
        });
    } catch {
        // 捨てられなくても、サーバーが正なので、次の読み込みで直る
    }
}

// サーバーからの更新の知らせ (Server-Sent Events)を受ける。「changed」の知らせで、callbackRef の Invoke を呼ぶ。dispose でつなぎをやめる
// 切れたときは、つなぎ直す前に、ログインが切れていないかを確かめる (Server-Sent Events は状態の番号を読めず、切れても、つなぎ直し続けるため)
export function openEvents(url, callbackRef) {
    let source = null;
    let stopped = false;
    const connect = () => {
        source = new EventSource(url);
        source.addEventListener("changed", () => callbackRef.invokeMethodAsync("Invoke"));
        source.onerror = () => {
            source.close();
            if (!stopped) {
                setTimeout(reconnect, 3000);
            }
        };
    };
    const reconnect = async () => {
        if (stopped) {
            return;
        }
        try {
            const response = await fetch("/api/auth/me", { credentials: "same-origin" });
            if (response.status === 401) {
                location.href = "/";
                return;
            }
        } catch {
            // 通信できないときは、次のつなぎ直しで確かめる
        }
        connect();
    };
    connect();
    return {
        dispose() {
            stopped = true;
            source?.close();
        },
    };
}

// 要素が先頭に近づいたら、callbackRef の Invoke を呼ぶ。dispose で購読をやめる
export function watchScrollTop(element, callbackRef) {
    const handler = () => {
        if (element.scrollTop < 80) {
            callbackRef.invokeMethodAsync("Invoke");
        }
    };
    element.addEventListener("scroll", handler, { passive: true });
    return {
        dispose() {
            element.removeEventListener("scroll", handler);
        },
    };
}

// 要素の中身の高さ。古いメッセージを足す前に測って、足したあとに、見ていた位置を保つ
export function getScrollHeight(element) {
    return element.scrollHeight;
}

// 古いメッセージを上に足したあと、見ていた位置が動かないよう、増えた高さの分だけ下げる
export function keepScrollAfterPrepend(element, previousHeight) {
    element.scrollTop += element.scrollHeight - previousHeight;
}

// 要素を、一番下までスクロールする
export function scrollToBottom(element) {
    element.scrollTop = element.scrollHeight;
}

// 要素の高さが縮んでも、一番下近くを見ていたら、一番下を保つ。キーボードを出したときや、入力欄が伸びたときに、最新のメッセージが押し出されないため。dispose でやめる
// 見ていた位置は、スクロールのたびに覚える。縮む動きでは、スクロールの位置が変わらないので、縮んだあとに測ると、押し出された位置で判定してしまう
export function keepBottomOnResize(element) {
    let stickToBottom = isNearBottom(element);
    const remember = () => {
        stickToBottom = isNearBottom(element);
    };
    const observer = new ResizeObserver(() => {
        if (stickToBottom) {
            element.scrollTop = element.scrollHeight;
        }
    });
    element.addEventListener("scroll", remember, { passive: true });
    observer.observe(element);
    return {
        dispose() {
            element.removeEventListener("scroll", remember);
            observer.disconnect();
        },
    };
}

// 要素が、ほぼ一番下を見ているか。新着が届いたとき、見ている位置を動かさないかを決める
export function isNearBottom(element) {
    return element.scrollHeight - element.scrollTop - element.clientHeight < 120;
}

// 全画面のシートや、画面いっぱいに固定した画面 (会話など)が開いている間、見えている範囲 (キーボードを除いた範囲)の上端と高さに合わせ、画面のずれを戻す。dispose でやめる
// iPhone は、キーボードを出すとき、入力欄が見えるように画面全体を動かし、閉じても戻さない端末がある。シートが開いている間は、裏の画面をスクロールさせない (css/app.css)ので、動いた分は、開いたときの位置に戻してよい
// 開いたときの位置は、裏の画面 (ノートの一覧など)のスクロール位置なので、0 ではなく、その位置へ戻す
// シートの css は、--sheet-top と --sheet-height を使う
const editableSelector = 'input:not([type="checkbox"]):not([type="radio"]):not([type="button"]), textarea, select, [contenteditable="true"]';
const keyboardSettleDelays = [0, 150, 400, 800];

export function watchSheetViewport() {
    const root = document.documentElement;
    const viewport = window.visualViewport;
    const openedX = window.scrollX;
    const openedY = window.scrollY;
    const timers = new Set();

    const fit = () => {
        if (viewport) {
            root.style.setProperty("--sheet-top", `${viewport.offsetTop}px`);
            root.style.setProperty("--sheet-height", `${viewport.height}px`);
        }
    };
    // 入力欄を触っている間は、キーボードが出ているので、iPhone の動きと取り合わない
    const restore = () => {
        if (document.activeElement?.matches(editableSelector)) {
            return;
        }
        if (window.scrollX !== openedX || window.scrollY !== openedY) {
            window.scrollTo(openedX, openedY);
        }
        fit();
    };
    // キーボードを閉じる動きは、入力欄を離れたあとも続くので、時間をおいて何度か確かめる
    const settle = () => {
        fit();
        for (const delay of keyboardSettleDelays) {
            const timer = setTimeout(() => {
                timers.delete(timer);
                restore();
            }, delay);
            timers.add(timer);
        }
    };

    fit();
    viewport?.addEventListener("resize", settle);
    viewport?.addEventListener("scroll", fit);
    document.addEventListener("focusin", settle);
    document.addEventListener("focusout", settle);
    return {
        dispose() {
            viewport?.removeEventListener("resize", settle);
            viewport?.removeEventListener("scroll", fit);
            document.removeEventListener("focusin", settle);
            document.removeEventListener("focusout", settle);
            timers.forEach(clearTimeout);
            timers.clear();
            root.style.removeProperty("--sheet-top");
            root.style.removeProperty("--sheet-height");
        },
    };
}

// id の要素を、画面の中ほどに出す。通知を押して開いたメッセージの位置まで、スクロールするため
export function scrollIntoViewById(id) {
    document.getElementById(id)?.scrollIntoView({ block: "center" });
}

// 入力欄で選んでいる範囲 (開始と終了の位置)と、選んでいる文字を返す。ノートの書式のボタンが使う
// 入力欄の値は改行を \n に直すので、位置と文字は、同じ値から取る
export function getSelection(element) {
    return [element.selectionStart, element.selectionEnd, element.value.substring(element.selectionStart, element.selectionEnd)];
}

// 入力欄の範囲を文字に置き換え、置き換えた文字を選び直す。入力の知らせも出して、画面の @bind に新しい値を渡す
export function replaceRange(element, start, end, text) {
    element.focus({ preventScroll: true });
    element.setRangeText(text, start, end, "select");
    element.dispatchEvent(new Event("input", { bubbles: true }));
}

// 入力欄で、文字を選んでいるかが変わったとき、.NET の Invoke を呼ぶ。dispose で購読をやめる
// iPhone の Safari は document に、ほかのブラウザは入力欄に、selectionchange を出すので、両方で受ける
export function watchHasSelection(element, dotNetRef) {
    let last = element.selectionStart !== element.selectionEnd;
    const handler = () => {
        if (document.activeElement !== element) {
            return;
        }
        const now = element.selectionStart !== element.selectionEnd;
        if (now !== last) {
            last = now;
            dotNetRef.invokeMethodAsync("Invoke");
        }
    };
    // 入力欄を離れている間は、選びが変わった知らせを受けないので、戻ったときに読み直して、ずれを残さない
    const resync = () => {
        last = element.selectionStart !== element.selectionEnd;
    };
    document.addEventListener("selectionchange", handler);
    element.addEventListener("selectionchange", handler);
    element.addEventListener("select", handler);
    element.addEventListener("focus", resync);
    return {
        dispose() {
            document.removeEventListener("selectionchange", handler);
            element.removeEventListener("selectionchange", handler);
            element.removeEventListener("select", handler);
            element.removeEventListener("focus", resync);
        },
    };
}
