using MmmSdk.Core.Components.Secrets;
using MmmSdk.Core.Components.Settings;
using WebPush;

namespace MmmBatch.Sending.Core.Notifiers;

/// <summary>
/// Web 版通知に使う、送り手の鍵 (VAPID 鍵)を持つ。
/// </summary>
/// <param name="settings">設定ストア。公開鍵の写しと連絡先を置く</param>
/// <param name="secrets">秘密の保管庫。鍵の組を置く</param>
/// <remarks>
/// 配達係 (アップル・グーグルなど)に、通知の送り手が同じだと示すための鍵の組。
/// 最初に必要になったときに作って保存し、以後は同じ鍵を使う。
/// 鍵を作り直すと、登録済みの宛先に送れなくなるので、端末ごとに登録し直す。
/// 公開鍵と秘密鍵は、資格情報マネージャーに、1 つの秘密として一緒に置く。DB のパスワードと同じ置き場で、設定ファイルには秘密鍵を置かない。
/// 片方だけが残って、組が食い違うことを防ぐため、別々には置かない。
/// 公開鍵は、手で写せるように、設定ファイルにも写しを置く。Web 版のサーバーの設定 <c>Web:PushPublicKey</c> に、同じ値を写して使う。
/// </remarks>
public sealed class WebPushKeyStore(ISettingsStore settings, ISecretStore secrets) : IDisposable
{
    /// <summary>公開鍵の写しを置く設定のキー</summary>
    public const string PublicKeySetting = "WebPush.PublicKey";

    /// <summary>連絡先を置く設定のキー</summary>
    public const string SubjectSetting = "WebPush.Subject";

    /// <summary>連絡先が未設定のときに使う値</summary>
    public const string DefaultSubject = "mailto:mmmtool@example.com";

    /// <summary>鍵の組を置く秘密の名前</summary>
    private const string KeysSecret = "MmmBatch.WebPushKeys";

    /// <summary>秘密の中で、公開鍵と秘密鍵を区切る文字。base64url の文字列に含まれない</summary>
    private const char Separator = ':';

    /// <summary>鍵の作成を 1 回にそろえる</summary>
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>送るときに使う、送り手の鍵と連絡先を取得する。無ければ作って保存する</summary>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>送り手の鍵と連絡先</returns>
    /// <exception cref="SendFailedException">鍵を読めない・保存できない。</exception>
    public async Task<VapidDetails> GetOrCreateAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var subject = settings.Get(SubjectSetting, DefaultSubject);
            var stored = secrets.Get(KeysSecret);
            if (TrySplit(stored, out var publicKey, out var privateKey))
            {
                await CopyPublicKeyAsync(publicKey, cancellationToken).ConfigureAwait(false);
                return new VapidDetails(subject, publicKey, privateKey);
            }

            // 保管庫に鍵の組が無いときだけ作る。作った組は、一度で保管庫に書く
            var created = VapidHelper.GenerateVapidKeys();
            secrets.Set(KeysSecret, $"{created.PublicKey}{Separator}{created.PrivateKey}");
            await CopyPublicKeyAsync(created.PublicKey, cancellationToken).ConfigureAwait(false);
            return new VapidDetails(subject, created.PublicKey, created.PrivateKey);
        }
        catch (SecretStoreException ex)
        {
            throw new SendFailedException("通知の鍵を、資格情報マネージャーに読み書きできませんでした。", ex);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public void Dispose() => _gate.Dispose();

    /// <summary>公開鍵の写しを、設定ファイルに置く。すでに同じ値なら何もしない</summary>
    /// <param name="publicKey">公開鍵</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>置き終えたことを表すタスク</returns>
    /// <remarks>
    /// 写しは便宜のためで、送るのに使わない。
    /// 設定ファイルを読めていない間は、書かない。読めなかっただけの既存の設定を、空で上書きしないため。
    /// 書けなかったときは、次に取得したときに、もう一度写す。
    /// </remarks>
    private async Task CopyPublicKeyAsync(string publicKey, CancellationToken cancellationToken)
    {
        if (settings.IsReadOnly || settings.Get(PublicKeySetting, "") == publicKey)
        {
            return;
        }
        await settings.SetAsync(PublicKeySetting, publicKey, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>秘密の文字列を、公開鍵と秘密鍵に分ける</summary>
    /// <param name="stored">保管庫から読んだ文字列。無ければ null</param>
    /// <param name="publicKey">公開鍵。分けられたときだけ入る</param>
    /// <param name="privateKey">秘密鍵。分けられたときだけ入る</param>
    /// <returns>2 つに分けられたら true</returns>
    private static bool TrySplit(string? stored, out string publicKey, out string privateKey)
    {
        publicKey = "";
        privateKey = "";
        var parts = stored?.Split(Separator);
        if (parts is not [{ Length: > 0 } publicPart, { Length: > 0 } privatePart])
        {
            return false;
        }
        publicKey = publicPart;
        privateKey = privatePart;
        return true;
    }
}
