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

// 端末の呼び名。パスキーの登録名と、通知の宛先の登録名に使う。iPhone と iPad は「iPhone」、それ以外は「ブラウザ」
export function deviceName() {
    return /iPhone|iPad/.test(navigator.userAgent) ? "iPhone" : "ブラウザ";
}

// ホーム画面のアプリとして開いているか。パスキー (js/webauthn.js)も使う
export function isStandalone() {
    return window.navigator.standalone === true || window.matchMedia("(display-mode: standalone)").matches;
}

// 通知の状態。unsupported = この環境では使えない / needs-home-screen = iPhone でホーム画面に追加していない / denied = 許可が拒否されている / ready = 登録できる
// subscribed は、このブラウザが今、宛先を持っているか。画面を開いたとき、サービスワーカーも登録しておく (押したときの待ちを短くするため)
export async function getPushStatus() {
    const isIos = /iPhone|iPad/.test(navigator.userAgent);
    if (isIos && !isStandalone()) {
        return { state: "needs-home-screen", subscribed: false };
    }
    if (!("serviceWorker" in navigator) || !("PushManager" in window) || !("Notification" in window)) {
        return { state: "unsupported", subscribed: false };
    }
    if (Notification.permission === "denied") {
        return { state: "denied", subscribed: false };
    }
    const registration = await registerWorker();
    const subscription = await registration.pushManager.getSubscription();
    return { state: "ready", subscribed: subscription !== null };
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

// 要素が、ほぼ一番下を見ているか。新着が届いたとき、見ている位置を動かさないかを決める
export function isNearBottom(element) {
    return element.scrollHeight - element.scrollTop - element.clientHeight < 120;
}

// id の要素を、画面の中ほどに出す。通知を押して開いたメッセージの位置まで、スクロールするため
export function scrollIntoViewById(id) {
    document.getElementById(id)?.scrollIntoView({ block: "center" });
}
