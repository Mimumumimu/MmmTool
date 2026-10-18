using System.Text;

namespace MmmToolWeb.Server.Hosting;

/// <summary>
/// 警告以上と、守りに関わる出来事 (<see cref="SecurityLog.Category"/>)を、日ごとのファイルに書くログの出力先。
/// </summary>
/// <param name="directoryPath">ログのファイルを置くフォルダ。</param>
/// <remarks>
/// 1 日のファイルが <see cref="MaxFileBytes"/> に達したら、その日の記録を止める。大量の拒否でディスクを埋められないようにするため。
/// 通常のアクセスの記録は、アクセスログ (W3C 形式)のファイルが受け持つ。
/// </remarks>
internal sealed class FileLoggerProvider(string directoryPath) : ILoggerProvider
{
    /// <summary>1 日のファイルの大きさの上限。</summary>
    private const long MaxFileBytes = 10 * 1024 * 1024;

    /// <summary>書き込みの排他用ロック。</summary>
    private readonly Lock _gate = new();

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName) => new FileLogger(this, categoryName);

    /// <inheritdoc />
    public void Dispose()
    {
    }

    /// <summary>
    /// 1 行を、今日のファイルに足す。
    /// </summary>
    /// <param name="text">足す内容。末尾の改行を含む。</param>
    private void Append(string text)
    {
        var now = DateTimeOffset.Now;
        var path = Path.Combine(directoryPath, $"server-{now:yyyy-MM-dd}.log");
        lock (_gate)
        {
            try
            {
                Directory.CreateDirectory(directoryPath);
                var length = File.Exists(path) ? new FileInfo(path).Length : 0;
                if (length >= MaxFileBytes)
                {
                    return;
                }
                if (length + Encoding.UTF8.GetByteCount(text) >= MaxFileBytes)
                {
                    text = $"[{now:yyyy-MM-dd HH:mm:ss.fff zzz}] 1 日の上限に達したので、今日の記録を止めます。{Environment.NewLine}";
                }
                File.AppendAllText(path, text, Encoding.UTF8);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // ログを書けないことで、サーバーを止めない。窓には出す
                Console.Error.WriteLine($"ログのファイルに書けませんでした: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 分類ごとの、ログの書き手。
    /// </summary>
    /// <param name="provider">書き込み先。</param>
    /// <param name="category">ログの分類の名前。</param>
    private sealed class FileLogger(FileLoggerProvider provider, string category) : ILogger
    {
        /// <inheritdoc />
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        /// <inheritdoc />
        public bool IsEnabled(LogLevel logLevel) =>
            logLevel != LogLevel.None && (logLevel >= LogLevel.Warning || (category == SecurityLog.Category && logLevel >= LogLevel.Information));

        /// <inheritdoc />
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }
            var message = formatter(state, exception).ReplaceLineEndings(" ");
            var text = new StringBuilder()
                .Append('[').Append(DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz")).Append("] ")
                .Append(ToLabel(logLevel)).Append(' ').Append(category).Append(": ").AppendLine(message);
            if (exception is not null)
            {
                text.AppendLine(exception.ToString());
            }
            provider.Append(text.ToString());
        }

        /// <summary>
        /// ログの段階の、ファイルに書く名前を返す。
        /// </summary>
        /// <param name="logLevel">ログの段階。</param>
        /// <returns>名前。</returns>
        private static string ToLabel(LogLevel logLevel) => logLevel switch
        {
            LogLevel.Warning => "警告",
            LogLevel.Error => "エラー",
            LogLevel.Critical => "重大",
            _ => "情報",
        };
    }
}
