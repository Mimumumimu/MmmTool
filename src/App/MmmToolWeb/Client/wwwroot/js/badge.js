// 読んでいない通知の一覧と、ホーム画面のアイコンの数字 (バッジ)を扱う。サービスワーカー (sw.js)と画面 (index.html)の両方が読み込む
// 一覧は、端末の控え (IndexedDB の mmmtoolweb.cache の kv)の unread に置く。ログアウトとアカウントの変更で、控えと一緒に消える
//   { keys: ["message:123", ...], high: { message: 130 } }
//   keys = 読んでいない通知の識別子で、種類:番号 の形。数字は、この件数
//   high = 種類ごとに、これまでに取り込んだ一番大きい番号。更新の知らせで同じメッセージが届き直しても、読んだものを未読に戻さないため
// バッジの機能や IndexedDB が使えない環境では、何もしない。通知の表示には影響しない

const cacheDbName = 'mmmtoolweb.cache';
const cacheStoreName = 'kv';
const unreadKey = 'unread';

// 数える種類。識別子の「種類:番号」の種類。数えない種類の通知は、数字に入れない
const countedKinds = ['message'];

// 端末の控えを開く。画面の js/browser.js も、これを使う
function openCacheDb() {
    return new Promise((resolve, reject) => {
        const request = indexedDB.open(cacheDbName, 1);
        request.onupgradeneeded = () => request.result.createObjectStore(cacheStoreName);
        request.onsuccess = () => resolve(request.result);
        request.onerror = () => reject(request.error);
    });
}

// 一覧を読み、change で書き換えて、書き戻す。読みと書きを 1 つの処理にして、続けて届いた通知が数え漏れにならないようにする
// 書き戻したあと、アイコンの数字を一覧の件数に合わせる
function changeUnread(change) {
    return new Promise((resolve, reject) => {
        openCacheDb().then((db) => {
            const transaction = db.transaction(cacheStoreName, 'readwrite');
            const store = transaction.objectStore(cacheStoreName);
            let state = null;
            const read = store.get(unreadKey);
            read.onsuccess = () => {
                state = {
                    keys: Array.isArray(read.result?.keys) ? read.result.keys : [],
                    high: read.result?.high && typeof read.result.high === 'object' ? read.result.high : {},
                };
                change(state);
                store.put(state, unreadKey);
            };
            transaction.oncomplete = () => {
                db.close();
                showUnreadBadge(state.keys.length);
                resolve(state);
            };
            transaction.onerror = () => reject(transaction.error);
            transaction.onabort = () => reject(transaction.error);
        }, reject);
    });
}

// アイコンの数字を直す。0 のときは消す
async function showUnreadBadge(count) {
    try {
        if (count > 0) {
            await self.navigator.setAppBadge?.(count);
        } else {
            await self.navigator.clearAppBadge?.();
        }
    } catch {
        // 数字を出せなくても、使うのに困らない
    }
}

// 識別子を「種類」と「番号」に分ける。数える種類でないとき、読めないときは null
function parseUnreadKey(key) {
    if (typeof key !== 'string') {
        return null;
    }
    const separator = key.indexOf(':');
    const kind = key.slice(0, separator);
    const id = Number(key.slice(separator + 1));
    return separator > 0 && countedKinds.includes(kind) && Number.isInteger(id) && id > 0 ? { kind, id } : null;
}

// 通知が届いたことを、サービスワーカーが数える。同じ識別子は 1 つにまとめる
async function addUnreadFromPush(key) {
    const parsed = parseUnreadKey(key);
    if (!parsed) {
        return;
    }
    try {
        await changeUnread((state) => {
            if (!state.keys.includes(key)) {
                state.keys.push(key);
            }
            state.high[parsed.kind] = Math.max(state.high[parsed.kind] ?? 0, parsed.id);
        });
    } catch {
        // 数えられなくても、通知は出す
    }
}

// 画面が取り込んだ新着を数える。ids = 数えるものの番号。maxId = 取り込んだ全部の中で一番大きい番号 (自分が送ったものも含む)
// 種類の high がまだ無いときは、基準を置くだけで、数えない (控えが無い端末で、昔のメッセージを未読にしないため)
async function addArrivedUnread(kind, ids, maxId) {
    if (!countedKinds.includes(kind)) {
        return;
    }
    try {
        await changeUnread((state) => {
            const high = state.high[kind];
            if (high !== undefined) {
                for (const id of ids) {
                    const key = `${kind}:${id}`;
                    if (id > high && !state.keys.includes(key)) {
                        state.keys.push(key);
                    }
                }
            }
            state.high[kind] = Math.max(high ?? 0, maxId, ...ids);
        });
    } catch {
        // 数えられなくても、使うのに困らない
    }
}

// 種類の high がまだ無いときだけ、基準を置く。最初の読み込みで呼ぶ
async function initUnreadHigh(kind, maxId) {
    try {
        await changeUnread((state) => {
            if (countedKinds.includes(kind) && state.high[kind] === undefined) {
                state.high[kind] = maxId;
            }
        });
    } catch {
        // 置けなくても、使うのに困らない
    }
}

// 読んだ通知を、一覧から外す
async function removeUnread(kind, ids) {
    try {
        const keys = new Set(ids.map((id) => `${kind}:${id}`));
        await changeUnread((state) => {
            state.keys = state.keys.filter((key) => !keys.has(key));
        });
    } catch {
        // 外せなくても、使うのに困らない
    }
}

// 種類の通知を、全部一覧から外す
async function clearUnread(kind) {
    try {
        await changeUnread((state) => {
            state.keys = state.keys.filter((key) => !key.startsWith(`${kind}:`));
        });
    } catch {
        // 外せなくても、使うのに困らない
    }
}

// 読んでいない通知の識別子を返す。無いとき、読めないときは空
async function readUnreadKeys() {
    try {
        const db = await openCacheDb();
        const value = await new Promise((resolve) => {
            const request = db.transaction(cacheStoreName, 'readonly').objectStore(cacheStoreName).get(unreadKey);
            request.onsuccess = () => resolve(request.result);
            request.onerror = () => resolve(null);
        });
        db.close();
        return Array.isArray(value?.keys) ? value.keys : [];
    } catch {
        return [];
    }
}

// 画面として読み込まれたときだけ、アイコンの数字を一覧の件数に合わせる (サービスワーカーには document が無い)
if (typeof document !== 'undefined') {
    readUnreadKeys().then((keys) => showUnreadBadge(keys.length));
}
