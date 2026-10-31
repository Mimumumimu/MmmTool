// 通知を受け取って表示する、サービスワーカー。サイトを閉じていても、ブラウザやホーム画面のアプリが動ける状態なら呼ばれる。
// 画面から直接は呼ばない。登録は js/browser.js の registerPush が行う。

const defaultTitle = 'MmmTool';

// 通知を押したときに、受け取った通知の一覧で、その通知を開く道
const receivedPath = '/notifications';

// 新しい版が入ったら、待たずに入れ替える
self.addEventListener('install', () => self.skipWaiting());
self.addEventListener('activate', (event) => event.waitUntil(self.clients.claim()));

// 読んでいない通知の一覧とアイコンの数字を扱う処理で、updateBadgeFromPush を使う。画面も同じファイルを読む
importScripts('/js/badge.js');

// 配達係から通知が届いたら、必ず 1 つ表示する。表示しない通知を受けると、ブラウザが購読を打ち切ることがあるため
// アプリの画面を前面で見ている間は、数字を直さない。画面が更新の知らせで取り込んで、数える
self.addEventListener('push', (event) => {
    const [title, options] = readNotification(event);
    event.waitUntil(Promise.all([
        self.registration.showNotification(title, options),
        hasVisibleWindow().then((visible) => (visible ? null : updateBadgeFromPush(options.data.key))),
    ]));
});

// 通知を押したら、通知の識別子があれば、一覧でその通知を開く。無ければ、リンクがあればそれを、無ければアプリを前に出す
self.addEventListener('notificationclick', (event) => {
    event.notification.close();
    const data = event.notification.data || {};
    event.waitUntil(data.key ? openApp(`${receivedPath}?item=${encodeURIComponent(data.key)}`) : data.url ? self.clients.openWindow(data.url) : openApp('/home'));
});

// 届いた内容を、表示するタイトルと設定にする。読めない内容でも、通知は出す
function readNotification(event) {
    let payload = {};
    try {
        payload = event.data ? event.data.json() : {};
    } catch {
        payload = {};
    }
    const key = typeof payload.key === 'string' ? payload.key : null;
    const options = {
        body: payload.body || '',
        icon: '/icon-192.png',
        data: { url: typeof payload.url === 'string' ? payload.url : null, key },
        timestamp: Date.now(),
    };
    // 同じ識別子の通知は、1 つにまとめる。送り直しで、同じ通知が重ならない
    if (key) {
        options.tag = key;
    }
    return [payload.title || defaultTitle, options];
}

// 前面で見られているアプリの窓があるか
async function hasVisibleWindow() {
    const windows = await self.clients.matchAll({ type: 'window', includeUncontrolled: true });
    return windows.some((client) => client.visibilityState === 'visible');
}

// 開いているアプリの窓があれば、指定の道へ移して前に出し、無ければ開く
async function openApp(path) {
    const windows = await self.clients.matchAll({ type: 'window', includeUncontrolled: true });
    if (windows.length > 0) {
        try {
            const focused = await windows[0].focus();
            return await focused.navigate(path);
        } catch {
            // 制御していない窓は、移せない。新しく開く
        }
    }
    return self.clients.openWindow(path);
}
