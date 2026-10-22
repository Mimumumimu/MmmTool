using MmmSdk.Core.Components.Storage;

namespace MmmTool.CliAssist.Core.Json;

/// <summary>よく使う文を JSON ファイルに保存する</summary>
/// <param name="store">JSON ファイルの読み書き</param>
public sealed class JsonCliQuickMessageRepository(IJsonFileStore store)
    : JsonCliListFileRepository<CliQuickMessage>(store, FileName, CliAssistJsonContext.Readable.ListCliQuickMessage), ICliQuickMessageRepository
{
    /// <summary>保存先のファイル名</summary>
    public const string FileName = "CliQuickMessages.json";
}
