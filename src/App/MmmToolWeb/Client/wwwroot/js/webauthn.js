// WebAuthn (パスキー)をブラウザで呼ぶ。サーバーとは Base64URL の文字列でやり取りし、ブラウザには ArrayBuffer で渡す

import { base64UrlToBytes, deviceName as browserDeviceName, isStandalone as browserIsStandalone } from './browser.js';

function fromBase64Url(text) {
    return base64UrlToBytes(text).buffer;
}

function toBase64Url(buffer) {
    const bytes = new Uint8Array(buffer);
    let binary = '';
    for (const b of bytes) {
        binary += String.fromCharCode(b);
    }
    return btoa(binary).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
}

/** パスキーを新しく作る。作らずに閉じられたときは null を返す。 */
export async function createCredential(options) {
    options.challenge = fromBase64Url(options.challenge);
    options.user.id = fromBase64Url(options.user.id);
    for (const c of options.excludeCredentials ?? []) {
        c.id = fromBase64Url(c.id);
    }
    let credential;
    try {
        credential = await navigator.credentials.create({ publicKey: options });
    } catch (e) {
        if (e.name === 'NotAllowedError' || e.name === 'AbortError') {
            return null;
        }
        throw e;
    }
    return {
        id: credential.id,
        rawId: toBase64Url(credential.rawId),
        type: credential.type,
        response: {
            attestationObject: toBase64Url(credential.response.attestationObject),
            clientDataJSON: toBase64Url(credential.response.clientDataJSON),
        },
        clientExtensionResults: credential.getClientExtensionResults(),
    };
}

/** 登録済みのパスキーで署名する。使わずに閉じられたときは null を返す。 */
export async function getCredential(options) {
    options.challenge = fromBase64Url(options.challenge);
    for (const c of options.allowCredentials ?? []) {
        c.id = fromBase64Url(c.id);
    }
    let credential;
    try {
        credential = await navigator.credentials.get({ publicKey: options });
    } catch (e) {
        if (e.name === 'NotAllowedError' || e.name === 'AbortError') {
            return null;
        }
        throw e;
    }
    return {
        id: credential.id,
        rawId: toBase64Url(credential.rawId),
        type: credential.type,
        response: {
            authenticatorData: toBase64Url(credential.response.authenticatorData),
            signature: toBase64Url(credential.response.signature),
            clientDataJSON: toBase64Url(credential.response.clientDataJSON),
            userHandle: credential.response.userHandle ? toBase64Url(credential.response.userHandle) : null,
        },
        clientExtensionResults: credential.getClientExtensionResults(),
    };
}

/** パスキーが使えるブラウザか。 */
export function isSupported() {
    return typeof window.PublicKeyCredential === 'function';
}

/** ホーム画面のアプリとして開いているか。 */
export function isStandalone() {
    return browserIsStandalone();
}

/** 端末の呼び名。通知の宛先の登録名と同じ決め方にするため、js/browser.js のものを使う。端末に古い画面が残っていても呼ばれるので、名前は消さない。 */
export function deviceName() {
    return browserDeviceName();
}
