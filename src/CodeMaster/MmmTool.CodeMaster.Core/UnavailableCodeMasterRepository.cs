namespace MmmTool.CodeMaster.Core;

/// <summary>
/// コードマスタを画面から編集できない保存先で、DB モードが使う。
/// </summary>
/// <remarks>DB モードのコードマスタは全員で共通なので、アプリからは編集しない。画面は、<see cref="CanEdit"/> が false の間は、編集の入口を出さない。</remarks>
public sealed class UnavailableCodeMasterRepository : ICodeMasterRepository
{
    /// <inheritdoc />
    public bool CanEdit => false;

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">編集は、ローカルモードだけで使える。</exception>
    public Task<IReadOnlyList<CodeMasterEntry>> LoadOptionsAsync(int featureNo, int categoryNo, CancellationToken cancellationToken = default)
        => throw NotAvailable();

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">編集は、ローカルモードだけで使える。</exception>
    public Task SaveNamesAsync(int featureNo, int categoryNo, IReadOnlyDictionary<int, string> namesByCodeNo, CancellationToken cancellationToken = default)
        => throw NotAvailable();

    /// <summary>使えないときの例外を作る</summary>
    /// <returns>使えないことを表す例外</returns>
    private static NotSupportedException NotAvailable() => new("コードマスタの編集は、ローカルモードだけで使えます。");
}
