using MmmSdk.Core.Components.Storage;

namespace MmmTool.CliAssist.Core.Json;

/// <summary>ターミナルへ移る文字列を JSON ファイルに保存する</summary>
/// <param name="store">JSON ファイルの読み書き</param>
public sealed class JsonCliFocusTriggerRepository(IJsonFileStore store)
    : JsonCliListFileRepository<CliFocusTrigger>(store, FileName, CliAssistJsonContext.Readable.ListCliFocusTrigger), ICliFocusTriggerRepository
{
    /// <summary>保存先のファイル名</summary>
    public const string FileName = "CliFocusTriggers.json";
}
