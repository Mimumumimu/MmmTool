namespace MmmTool.DayContent.Core;

/// <summary>
/// ある期間と人について、日ごとの内容 (見出しの下に出す文)を返す、機能の入口。
/// </summary>
/// <remarks>
/// 内容を出す側 (ノートなど)は、この契約だけを知り、提供する機能 (作業管理など)の名前を持たない。
/// 提供する機能が、自分の登録に 1 行足して、実装を登録する。返すのは HTML ではなく、文字のデータ。
/// </remarks>
public interface IDayContentSource
{
    /// <summary>内容の識別</summary>
    /// <remarks>設定のキーにも使う。変えると、保存済みのオン・オフが外れる。</remarks>
    string Key { get; }

    /// <summary>画面に出す、内容の名前</summary>
    string Title { get; }

    /// <summary>この内容を出せる機能のキー</summary>
    /// <remarks>機能のオン・オフに合わせる。機能のオン・オフが無いときは null。</remarks>
    string? FeatureKey { get; }

    /// <summary>この内容を出せる機能の機能番号で、<c>CodeFeatures</c> の番号</summary>
    /// <remarks>サーバーモードで、管理者がその機能を無効にしたユーザーには、この内容を出さない。管理者が決められない機能は null。</remarks>
    int? FeatureNo => null;

    /// <summary>画面に出す、真偽の選択肢 (チェックとして並べる)</summary>
    IReadOnlyList<DayContentOption> Options { get; }

    /// <summary>内容が変わった</summary>
    /// <remarks>
    /// 使う側 (ノートなど)は、見えている間だけ購読して、変わったら読み直す。見えなくなったら、購読を解除する。
    /// 通知しない提供元は、実装しなくてよい。何もしない既定の実装がある。
    /// どのスレッドからでも通知してよい。使う側が、画面のスレッドへ戻す。
    /// </remarks>
    event EventHandler? Changed
    {
        add { }
        remove { }
    }

    /// <summary>期間の開始日の既定を返す</summary>
    /// <param name="to">期間の終了日 (報告日)</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>開始日の既定 (自分の分から決める)</returns>
    Task<DateOnly> GetDefaultFromAsync(DateOnly to, CancellationToken cancellationToken = default);

    /// <summary>期間のうちに、内容がある人を返す</summary>
    /// <param name="from">期間の開始日</param>
    /// <param name="to">期間の終了日</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>内容がある人のユーザーの番号。名前は、使う側が引く</returns>
    Task<IReadOnlyList<int>> GetPeopleAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken = default);

    /// <summary>期間と人を指定して、内容を返す</summary>
    /// <param name="from">期間の開始日</param>
    /// <param name="to">期間の終了日 (その日の内容を出す日)</param>
    /// <param name="userId">人の番号。null は、今のユーザー</param>
    /// <param name="options">選択肢の値で、キーは <see cref="DayContentOption.Key"/>。無いキーは、既定値</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>見出しの下に出す文</returns>
    Task<string> GetTextAsync(DateOnly from, DateOnly to, int? userId, IReadOnlyDictionary<string, bool> options, CancellationToken cancellationToken = default);

    /// <summary>期間と複数の人を指定して、人ごとの内容を、まとめて返す</summary>
    /// <param name="from">期間の開始日</param>
    /// <param name="to">期間の終了日 (その日の内容を出す日)</param>
    /// <param name="userIds">人の番号</param>
    /// <param name="options">選択肢の値で、キーは <see cref="DayContentOption.Key"/>。無いキーは、既定値</param>
    /// <param name="cancellationToken">キャンセルを監視するトークン</param>
    /// <returns>人の番号ごとの、見出しの下に出す文</returns>
    /// <remarks>人の数だけ <see cref="GetTextAsync"/> を呼ぶ代わりに使う。提供元は、保存先から 1 回で読んで、人ごとに分ける。</remarks>
    Task<IReadOnlyDictionary<int, string>> GetTextsAsync(DateOnly from, DateOnly to, IReadOnlyCollection<int> userIds, IReadOnlyDictionary<string, bool> options, CancellationToken cancellationToken = default);
}
