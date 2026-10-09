using MmmSdk.Core.Components.Storage;

namespace MmmTool.CliAssist.Core;

/// <summary>よく使う文をメモリに持ち、変更を保存する。アプリ全体で 1 つ。</summary>
/// <param name="repository">よく使う文の保存先</param>
public sealed class CliQuickMessageService(ICliQuickMessageRepository repository)
{
    /// <summary>読み込みの結果</summary>
    /// <remarks>
    /// ファイルを読めなかったとき (ロック・権限など)は、元のファイルを上書きで消さないよう、編集と保存を止める。
    /// 壊れていたときは退避済みなので止めない (空の一覧から、新しいファイルを作れる)。
    /// </remarks>
    private readonly LoadStatus _status = new();

    /// <summary>よく使う文 (本文が空の項目は除く)</summary>
    public IReadOnlyList<CliQuickMessage> Messages { get; private set; } = [];

    /// <summary>よく使う文が変わった (保存した)</summary>
    public event Action? Changed;

    /// <summary>読み込みに失敗したときのメッセージ。</summary>
    public string? LoadError => _status.LoadError;

    /// <summary>壊れていたファイルを退避したときのメッセージ。</summary>
    public string? RecoveryMessage => _status.RecoveryMessage;

    /// <summary>編集して保存できるか (読み込みに失敗していないとき)</summary>
    public bool CanEdit => !_status.HasFailed;

    /// <summary>よく使う文を読み込む</summary>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>読み込みの完了を表すタスク</returns>
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var (messages, recoveryMessage) = await repository.LoadAsync(cancellationToken);
            Messages = Normalize(messages);
            // 壊れていたときの SDK のメッセージは「作り直しました」と言うが、このファイルは読み込みでは作り直さない (手で書いたものなので)。直し方を案内する
            _status.Succeeded(recoveryMessage is null ? null : CreateRecoveryMessage(recoveryMessage));
        }
        catch (DataFileException ex)
        {
            _status.Failed(ex, $"{ex.Message}\nよく使う文は表示されません。");
        }
    }

    /// <summary>よく使う文を保存して、反映する</summary>
    /// <param name="messages">保存するよく使う文 (本文が空の項目は除く)</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>保存の完了を表すタスク</returns>
    /// <remarks>読み込みに失敗しているときは何もしない (読めなかったファイルを上書きしないため)。保存に失敗したときは、メモリの内容も変えない。</remarks>
    /// <exception cref="DataFileException">保存に失敗した。</exception>
    public async Task SaveAsync(IEnumerable<CliQuickMessage> messages, CancellationToken cancellationToken = default)
    {
        if (!CanEdit)
        {
            return;
        }

        var normalized = Normalize(messages);
        await repository.SaveAsync(normalized, cancellationToken);
        Messages = normalized;
        Changed?.Invoke();
    }

    /// <summary>本文が空の項目を除き、表示名の空白を整える</summary>
    /// <param name="messages">よく使う文</param>
    /// <returns>整えたよく使う文 (元の並びのまま)</returns>
    /// <remarks>JSON に null や、本文の無い項目が書かれていても、チップには出さない。</remarks>
    private static List<CliQuickMessage> Normalize(IEnumerable<CliQuickMessage> messages)
        => [.. messages
            .Where(message => message is { Text: { } text } && !string.IsNullOrWhiteSpace(text))
            .Select(message => new CliQuickMessage
            {
                Label = string.IsNullOrWhiteSpace(message.Label) ? null : message.Label.Trim(),
                Text = message.Text,
            })];

    /// <summary>壊れたファイルを退避したときの、画面に出すメッセージを作る</summary>
    /// <param name="storeMessage">ファイル保存の部品が作ったメッセージ (1 行目は退避の報告、2 行目以降は JSON の誤りの内容)</param>
    /// <returns>誤りの内容と、直し方を案内するメッセージ</returns>
    private static string CreateRecoveryMessage(string storeMessage)
    {
        var lines = storeMessage.Split('\n', 2);
        var detail = lines.Length > 1 ? $"\n{lines[1]}" : string.Empty;
        return $"よく使う文のファイルが壊れていたため、別の名前に退避しました。{detail}\n退避したファイルを直し、名前を CliQuickMessages.json に戻して、アプリを再起動してください。よく使う文は表示されません。";
    }
}
