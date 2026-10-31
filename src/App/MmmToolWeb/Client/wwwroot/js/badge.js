// 読んでいない通知の一覧と、ホーム画面のアイコンの数字 (バッジ)を扱う。サービスワーカー (sw.js)と画面 (index.html)の両方が読み込む
// 一覧は、端末の控え (IndexedDB の mmmtoolweb.cache の kv)の unread に置く。ログアウトとアカウントの変更で、控えと一緒に消える
//   { keys: ["note:123", ...], high: { note: 130 }, counts: { message: 3 } }
//   keys = 端末が数える種類の、読んでいない通知の識別子で、種類:番号 の形。この件数が数字に入る
//   high = 種類ごとに、これまでに取り込んだ一番大きい番号。更新の知らせで同じ通知が届き直しても、読んだものを未読に戻さないため
//   counts = サーバーが数える種類の、直近に聞いた件数。この合計も数字に入る。読んだ位置はサーバーが持つので、端末は数えない
// バッジの機能や IndexedDB が使えない環境では、何もしない。通知の表示には影響しない

const cacheDbName = 'mmmtoolweb.cache';
const cacheStoreName = 'kv';
const unreadKey = 'unread';

// 端末が数える種類。識別子の「種類:番号」の種類。数えない種類の通知は、数字に入れない
// note は、通知が届いたときだけ数える。画面からは入れない。ノートのページを開いたときに、全部外す
const countedKinds = ['note'];

// サーバーが数える種類と、件数を聞く道。メッセージは、読んだ位置が DB にあるので、端末では数えない
const serverCountedKinds = { message: '/api/messages/unread-count' };

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
                state = normalizeUnread(read.result);
                change(state);
                store.put(state, unreadKey);
            };
            transaction.oncomplete = () => {
                db.close();
                showUnreadBadge(unreadTotal(state));
                resolve(state);
            };
            transaction.onerror = () => reject(transaction.error);
            transaction.onabort = () => reject(transaction.error);
        }, reject);
    });
}

// 控えから読んだ値を、一覧の形に整える。無い・壊れているときは、空の一覧にする
function normalizeUnread(value) {
    const object = (item) => (item && typeof item === 'object' && !Array.isArray(item) ? item : {});
    return {
        // 端末が数える種類だけ残す。前のバージョンが持っていたメッセージの識別子は、いま数えず、サーバーの件数と二重にならないよう、ここで捨てる
        keys: Array.isArray(value?.keys) ? value.keys.filter((key) => parseUnreadKey(key) !== null) : [],
        high: object(value?.high),
        counts: object(value?.counts),
    };
}

// アイコンの数字にする合計。端末が数える通知の件数と、サーバーが数える件数
function unreadTotal(state) {
    const server = Object.values(state.counts).reduce((sum, count) => sum + (Number.isInteger(count) && count > 0 ? count : 0), 0);
    return state.keys.length + server;
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

// サーバーが数える種類の件数を、置く。画面が、サーバーから読んだ件数を渡す
async function setServerUnread(kind, count) {
    if (!(kind in serverCountedKinds) || !Number.isInteger(count) || count < 0) {
        return;
    }
    try {
        await changeUnread((state) => {
            state.counts[kind] = count;
        });
    } catch {
        // 置けなくても、使うのに困らない
    }
}

// サーバーが数える種類の件数を、サーバーに聞いて置く。ログインが切れているなど、聞けないときは、前の件数のまま
async function refreshServerUnread(onlyKind) {
    for (const [kind, path] of Object.entries(serverCountedKinds)) {
        if (onlyKind && onlyKind !== kind) {
            continue;
        }
        try {
            const response = await fetch(path, { credentials: 'same-origin' });
            if (response.ok) {
                await setServerUnread(kind, (await response.json()).count);
            }
        } catch {
            // 聞けなくても、通知とバッジの表示には影響しない
        }
    }
}

// 通知が届いたとき、サービスワーカーが数字を直す。端末が数える種類は一覧に入れ、サーバーが数える種類は件数を聞き直す
async function updateBadgeFromPush(key) {
    const kind = typeof key === 'string' ? key.slice(0, key.indexOf(':')) : '';
    if (kind in serverCountedKinds) {
        await refreshServerUnread(kind);
    } else {
        await addUnreadFromPush(key);
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

// 一覧と件数を読む。無いとき、読めないときは空
async function readUnreadState() {
    try {
        const db = await openCacheDb();
        const value = await new Promise((resolve) => {
            const request = db.transaction(cacheStoreName, 'readonly').objectStore(cacheStoreName).get(unreadKey);
            request.onsuccess = () => resolve(request.result);
            request.onerror = () => resolve(null);
        });
        db.close();
        return normalizeUnread(value);
    } catch {
        return normalizeUnread(null);
    }
}

// 端末が数える、読んでいない通知の識別子を返す。無いとき、読めないときは空
async function readUnreadKeys() {
    return (await readUnreadState()).keys;
}

// 画面として読み込まれたときだけ、アイコンの数字を直す (サービスワーカーには document が無い)
// 先に前回の件数で出し、そのあと、サーバーが数える種類の件数を聞き直す
if (typeof document !== 'undefined') {
    readUnreadState().then((state) => {
        showUnreadBadge(unreadTotal(state));
        return refreshServerUnread();
    });
}
