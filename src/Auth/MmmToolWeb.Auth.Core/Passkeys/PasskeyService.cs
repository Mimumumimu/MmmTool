using System.Security.Cryptography;
using Fido2NetLib;
using Fido2NetLib.Objects;

namespace MmmToolWeb.Auth.Core.Passkeys;

/// <summary>
/// パスキーの登録とログイン (WebAuthn の手続き)。人の特定とログイン状態の発行は呼び出し側が行う。
/// </summary>
/// <param name="fido2">WebAuthn の検証を行うライブラリ。</param>
/// <param name="repository">パスキーの保存先。</param>
/// <param name="ceremonies">手続きの途中の状態の置き場。</param>
public sealed class PasskeyService(IFido2 fido2, IAppUserPasskeyRepository repository, PasskeyCeremonyStore ceremonies)
{
    /// <summary>手続きの途中の状態 (登録)。</summary>
    private sealed record RegistrationCeremony(int AppUserId, string DeviceName, CredentialCreateOptions Options);

    /// <summary>手続きの途中の状態 (ログイン)。</summary>
    private sealed record LoginCeremony(AssertionOptions Options);

    /// <summary>
    /// パスキーの登録を始める。
    /// </summary>
    /// <param name="appUserId">登録する人のユーザーの番号。</param>
    /// <param name="userName">パスキーに表示するユーザー名。</param>
    /// <param name="displayName">パスキーに表示する名前。</param>
    /// <param name="deviceName">端末の呼び名。</param>
    /// <param name="cancellationToken">キャンセルの通知。</param>
    /// <returns>ブラウザに渡す選択肢と、完了の呼び出しに渡す番号。</returns>
    public async Task<PasskeyStart> StartRegistrationAsync(
        int appUserId, string userName, string displayName, string deviceName, CancellationToken cancellationToken)
    {
        var existing = await repository.GetByUserAsync(appUserId, cancellationToken);
        var user = new Fido2User
        {
            Id = existing.Count > 0 ? existing[0].UserHandle : RandomNumberGenerator.GetBytes(32),
            Name = userName,
            DisplayName = displayName,
        };
        var options = fido2.RequestNewCredential(new RequestNewCredentialParams
        {
            User = user,
            ExcludeCredentials = [.. existing.Select(p => new PublicKeyCredentialDescriptor(p.CredentialId))],
            AuthenticatorSelection = new AuthenticatorSelection
            {
                ResidentKey = ResidentKeyRequirement.Required,
                UserVerification = UserVerificationRequirement.Required,
            },
            AttestationPreference = AttestationConveyancePreference.None,
        });
        var id = ceremonies.Put(new RegistrationCeremony(appUserId, deviceName, options));
        return new PasskeyStart(id, options.ToJson());
    }

    /// <summary>
    /// パスキーの登録を完了して、保存する。
    /// </summary>
    /// <param name="ceremonyId"><see cref="StartRegistrationAsync"/> が返した番号。</param>
    /// <param name="response">ブラウザが返した結果。</param>
    /// <param name="cancellationToken">キャンセルの通知。</param>
    /// <returns>登録した人のユーザーの番号。番号が違う・期限切れのときは <c>null</c>。</returns>
    /// <exception cref="Fido2VerificationException">ブラウザの結果の検証に失敗したとき。</exception>
    public async Task<int?> FinishRegistrationAsync(
        string ceremonyId, AuthenticatorAttestationRawResponse response, CancellationToken cancellationToken)
    {
        var ceremony = ceremonies.Take<RegistrationCeremony>(ceremonyId);
        if (ceremony is null)
        {
            return null;
        }
        var credential = await fido2.MakeNewCredentialAsync(
            new MakeNewCredentialParams
            {
                AttestationResponse = response,
                OriginalOptions = ceremony.Options,
                IsCredentialIdUniqueToUserCallback = async (args, token) =>
                    await repository.FindByCredentialIdAsync(args.CredentialId, token) is null,
            },
            cancellationToken);
        await repository.AddAsync(
            new AppUserPasskey(
                0, ceremony.AppUserId, credential.Id, credential.PublicKey, credential.SignCount,
                ceremony.Options.User.Id, ceremony.DeviceName, DateTime.UtcNow),
            cancellationToken);
        return ceremony.AppUserId;
    }

    /// <summary>
    /// パスキーでのログインを始める (ユーザー名を入力させない方式)。
    /// </summary>
    /// <returns>ブラウザに渡す選択肢と、完了の呼び出しに渡す番号。</returns>
    public PasskeyStart StartLogin()
    {
        var options = fido2.GetAssertionOptions(new GetAssertionOptionsParams
        {
            AllowedCredentials = [],
            UserVerification = UserVerificationRequirement.Required,
        });
        var id = ceremonies.Put(new LoginCeremony(options));
        return new PasskeyStart(id, options.ToJson());
    }

    /// <summary>
    /// パスキーでのログインを完了して、人を特定する。
    /// </summary>
    /// <param name="ceremonyId"><see cref="StartLogin"/> が返した番号。</param>
    /// <param name="response">ブラウザが返した結果。</param>
    /// <param name="cancellationToken">キャンセルの通知。</param>
    /// <returns>ログインした人のユーザーの番号。番号が違う・期限切れ・未登録のパスキーのときは <c>null</c>。</returns>
    /// <exception cref="Fido2VerificationException">ブラウザの結果の検証に失敗したとき。</exception>
    public async Task<int?> FinishLoginAsync(
        string ceremonyId, AuthenticatorAssertionRawResponse response, CancellationToken cancellationToken)
    {
        var ceremony = ceremonies.Take<LoginCeremony>(ceremonyId);
        if (ceremony is null)
        {
            return null;
        }
        var passkey = await repository.FindByCredentialIdAsync(response.RawId, cancellationToken);
        if (passkey is null)
        {
            return null;
        }
        var result = await fido2.MakeAssertionAsync(
            new MakeAssertionParams
            {
                AssertionResponse = response,
                OriginalOptions = ceremony.Options,
                StoredPublicKey = passkey.PublicKey,
                StoredSignatureCounter = passkey.SignCount,
                IsUserHandleOwnerOfCredentialIdCallback = (args, _) =>
                    Task.FromResult(passkey.UserHandle.AsSpan().SequenceEqual(args.UserHandle)),
            },
            cancellationToken);
        await repository.UpdateUsageAsync(passkey.Id, result.SignCount, DateTime.UtcNow, cancellationToken);
        return passkey.AppUserId;
    }
}
