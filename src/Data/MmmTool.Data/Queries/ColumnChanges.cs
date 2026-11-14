namespace MmmTool.Data.Queries;

/// <summary>更新で変えた列</summary>
/// <remarks>
/// 値が違う列だけを集めて、<c>UPDATE</c> の <c>SET</c> 句にする。
/// パラメーターの名前は <c>@列名</c>。
/// </remarks>
public sealed class ColumnChanges
{
    /// <summary>代入の並び</summary>
    private readonly List<string> _sets = [];

    /// <summary>パラメーター</summary>
    private readonly List<(string, object)> _parameters = [];

    /// <summary>変えた列が無いか</summary>
    public bool IsEmpty => _sets.Count == 0;

    /// <summary>SET 句の中身</summary>
    public string SetClause => string.Join(", ", _sets);

    /// <summary>パラメーター</summary>
    public IEnumerable<(string, object)> Parameters => _parameters;

    /// <summary>値が違うときだけ列を足す</summary>
    /// <param name="column">列の名前</param>
    /// <param name="before">更新前の値</param>
    /// <param name="after">更新後の値</param>
    public void Add(string column, object before, object after)
    {
        if (!Equals(before, after))
        {
            Set(column, after);
        }
    }

    /// <summary>列を足す</summary>
    /// <param name="column">列の名前</param>
    /// <param name="value">新しい値</param>
    public void Set(string column, object value)
    {
        _sets.Add($"{column} = @{column}");
        _parameters.Add(($"@{column}", value));
    }
}
