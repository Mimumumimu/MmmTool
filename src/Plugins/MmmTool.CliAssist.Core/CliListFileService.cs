using MmmSdk.Core.Components.Storage;

namespace MmmTool.CliAssist.Core;

/// <summary>編集画面で書くか、手で書く一覧のファイルをメモリに持ち、変更を保存する土台。アプリ全体で 1 つ。</summary>
/// <typeparam name="TItem">一覧の項目の型</typeparam>
/// <param name="repository">一覧の保存先</param>
/// <param name="fileName">ファイル名 (壊れて退避したときに、名前を戻す先として案内する)</param>
/// <param name="subject">一覧の呼び名 (例: よく使う文)</param>
/// <param name="unavailable">読み込めなかったときに、結果として添える文</param>
/// <remarks>
/// ファイルを読めなかったとき (ロック・権限など)は、元のファイルを上書きで消さないよう、編集と保存を止める。
/// 壊れていたときは退避済みなので止めない。空の一覧から、新しいファイルを作れる。
/// </remarks>
public abstract class CliListFileService<TItem>(ICliListFileRepository<TItem> repository, string fileName, string subject, string unavailable)
{
    /// <summary>読み込みの結果</summary>
    private readonly LoadStatus _status = new();

    /// <summary>一覧 (空の項目は除く)</summary>
    public IReadOnlyList<TItem> Items { get; private set; } = [];

    /// <summary>一覧が変わった (保存した)</summary>
    public event Action? Changed;

    /// <summary>読み込みに失敗したときのメッセージ。</summary>
    public string? LoadError => _status.LoadError;

    /// <summary>壊れていたファイルを退避したときのメッセージ。</summary>
    public string? RecoveryMessage => _status.RecoveryMessage;

    /// <summary>編集して保存できるか (読み込みに失敗していないとき)</summary>
    public bool CanEdit => !_status.HasFailed;

    /// <summary>一覧を読み込む</summary>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>読み込みの完了を表すタスク</returns>
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var (items, recoveryMessage) = await repository.LoadAsync(cancellationToken);
            Items = Normalize(items);
            // 壊れていたときの SDK のメッセージは「作り直しました」と言うが、手で書いてもよいファイルなので、読み込みでは作り直さない。直し方を案内する
            _status.Succeeded(recoveryMessage is null ? null : CreateRecoveryMessage(recoveryMessage));
        }
        catch (DataFileException ex)
        {
            _status.Failed(ex, $"{ex.Message}\n{unavailable}");
        }
    }

    /// <summary>一覧を保存して、反映する</summary>
    /// <param name="items">保存する一覧 (空の項目は除く)</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>保存の完了を表すタスク</returns>
    /// <remarks>読み込みに失敗しているときは、読めなかったファイルを上書きしないよう、何もしない。保存に失敗したときは、メモリの内容も変えない。</remarks>
    /// <exception cref="DataFileException">保存に失敗した。</exception>
    public async Task SaveAsync(IEnumerable<TItem> items, CancellationToken cancellationToken = default)
    {
        if (!CanEdit)
        {
            return;
        }

        var normalized = Normalize(items);
        await repository.SaveAsync(normalized, cancellationToken);
        Items = normalized;
        Changed?.Invoke();
    }

    /// <summary>空の項目を除き、項目を整える</summary>
    /// <param name="items">一覧</param>
    /// <returns>整えた一覧 (元の並びのまま)</returns>
    /// <remarks>JSON に null や、中身の無い項目が書かれていても、使わない。</remarks>
    protected abstract List<TItem> Normalize(IEnumerable<TItem> items);

    /// <summary>壊れたファイルを退避したときの、画面に出すメッセージを作る</summary>
    /// <param name="storeMessage">ファイル保存の部品が作ったメッセージ (1 行目は退避の報告、2 行目以降は JSON の誤りの内容)</param>
    /// <returns>誤りの内容と、直し方を案内するメッセージ</returns>
    private string CreateRecoveryMessage(string storeMessage)
    {
        var lines = storeMessage.Split('\n', 2);
        var detail = lines.Length > 1 ? $"\n{lines[1]}" : string.Empty;
        return $"{subject}のファイルが壊れていたため、別の名前に退避しました。{detail}\n退避したファイルを直し、名前を {fileName} に戻して、アプリを再起動してください。{unavailable}";
    }
}
