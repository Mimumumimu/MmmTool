using System.Globalization;
using System.Text;

namespace MmmToolWeb.Server.Hosting;

/// <summary>
/// 断った接続元の一覧を、ファイルに保存する。起動時に読み込み、変わったときだけ 1 分ごとに書き直し、止めるときにも書く。
/// </summary>
/// <param name="guard">探りの数え方と、断った接続元の一覧。</param>
/// <param name="filePath">一覧のファイル。</param>
internal sealed class RejectedClientListService(ProbeGuard guard, string filePath) : BackgroundService
{
    /// <summary>書き直す間隔。</summary>
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    /// <summary>日時の書き方。</summary>
    private const string TimeFormat = "yyyy-MM-dd HH:mm:ss zzz";

    /// <summary>項目の区切り。</summary>
    private const char Separator = '\t';

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Load();
        using var timer = new PeriodicTimer(Interval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                guard.Prune(DateTimeOffset.UtcNow);
                if (guard.TakeChanged())
                {
                    Save();
                }
            }
        }
        catch (OperationCanceledException)
        {
            // サーバーを止めるときの、正常な終わり方
        }

        // 止めるまでの変わった分を、書き残す
        if (guard.TakeChanged())
        {
            Save();
        }
    }

    /// <summary>
    /// 保存してあった一覧を読み込む。読めなかった行は飛ばす。
    /// </summary>
    private void Load()
    {
        if (!File.Exists(filePath))
        {
            return;
        }
        try
        {
            var rows = new List<RejectedClientInfo>();
            foreach (var line in File.ReadLines(filePath, Encoding.UTF8).Skip(1))
            {
                var fields = line.Split(Separator);
                if (fields.Length == 5
                    && DateTimeOffset.TryParse(fields[1], CultureInfo.InvariantCulture, out var firstAt)
                    && DateTimeOffset.TryParse(fields[2], CultureInfo.InvariantCulture, out var lastAt)
                    && int.TryParse(fields[3], CultureInfo.InvariantCulture, out var count))
                {
                    rows.Add(new RejectedClientInfo(fields[0], firstAt, lastAt, count, fields[4]));
                }
            }
            guard.Restore(rows);
        }
        catch (IOException ex)
        {
            Console.Error.WriteLine($"断った接続元の一覧を読めませんでした: {ex.Message}");
        }
    }

    /// <summary>
    /// 一覧を、ファイルに書き直す。
    /// </summary>
    private void Save()
    {
        var text = new StringBuilder()
            .AppendJoin(Separator, "接続元", "最初に断った日時", "最後に断った日時", "断った回数", "最後の道").AppendLine();
        foreach (var row in guard.Snapshot())
        {
            text.AppendJoin(
                Separator,
                row.Address,
                row.FirstAt.ToLocalTime().ToString(TimeFormat, CultureInfo.InvariantCulture),
                row.LastAt.ToLocalTime().ToString(TimeFormat, CultureInfo.InvariantCulture),
                row.Count.ToString(CultureInfo.InvariantCulture),
                row.LastPath).AppendLine();
        }
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            // 一時ファイルに書いてから置き換える (書いている途中で止まっても、前のファイルが壊れないため)
            var temporaryPath = filePath + ".tmp";
            File.WriteAllText(temporaryPath, text.ToString(), Encoding.UTF8);
            File.Move(temporaryPath, filePath, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // 一覧を書けないことで、サーバーを止めない。窓には出す
            Console.Error.WriteLine($"断った接続元の一覧を書けませんでした: {ex.Message}");
        }
    }
}
