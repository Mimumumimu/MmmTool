using MmmSdk.Core.Components.Storage;

namespace MmmTool.CliAssist.Core.Json;

/// <summary>定型コマンドを JSON ファイルに保存する</summary>
/// <param name="store">JSON ファイルの読み書き</param>
/// <param name="createDefaults">ファイルが無い・壊れていたときに作る、既定の定型コマンドを作る処理 (既定の中身と、初期設定 (環境・使うツール)の選び方はアプリが決める)</param>
/// <remarks><paramref name="createDefaults"/> は、<see cref="LoadAsync"/> を呼んだスレッド (UI スレッド)で呼ぶ (アプリが初期設定のダイアログを出すため)。</remarks>
public sealed class JsonCliCommandRepository(IJsonFileStore store, Func<CancellationToken, Task<CliCommandSet>> createDefaults) : ICliCommandRepository
{
    /// <summary>保存先のファイル名</summary>
    private const string FileName = "CliCommands.json";

    /// <inheritdoc />
    public async Task<DataLoadResult<CliCommandSet>> LoadAsync(CancellationToken cancellationToken = default)
    {
        var typeInfo = CliAssistJsonContext.Readable.CliCommandSet;

        if (!store.Exists(FileName))
        {
            return new(await WriteDefaultsAsync(cancellationToken));
        }

        var result = await store.ReadAsync(FileName, typeInfo, cancellationToken);
        if (result.RecoveryMessage is not null)
        {
            // 壊れたファイルは退避済みなので、無いときと同じく既定で作り直す
            return new(await WriteDefaultsAsync(cancellationToken), result.RecoveryMessage);
        }

        // 中身が null (手修正で空にした等)でも既定で上書きはせず、空のまま扱う
        var set = result.Value ?? new CliCommandSet();
        // JSON に null と書かれていたときも、空として扱う
        set.Shell ??= [];
        set.Session ??= [];
        return new(set);
    }

    /// <inheritdoc />
    public Task SaveAsync(CliCommandSet commandSet, CancellationToken cancellationToken = default)
        => store.WriteAsync(FileName, commandSet, CliAssistJsonContext.Readable.CliCommandSet, cancellationToken);

    /// <summary>既定の定型コマンドを作って保存する</summary>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>保存した既定の定型コマンド</returns>
    private async Task<CliCommandSet> WriteDefaultsAsync(CancellationToken cancellationToken)
    {
        var defaults = await createDefaults(cancellationToken);
        await store.WriteAsync(FileName, defaults, CliAssistJsonContext.Readable.CliCommandSet, cancellationToken);
        return defaults;
    }
}
