namespace MmmTool.Services.Terminal;

/// <summary>
/// 擬似コンソール上で動くシェル 1 つ分のセッション。
/// </summary>
public interface ITerminalSession : IDisposable
{
    /// <summary>起動するコマンドライン（Start 前に設定する）。</summary>
    string CommandLine { get; set; }

    /// <summary>起動時の作業ディレクトリ（Start 前に設定する）。</summary>
    string WorkingDirectory { get; set; }

    /// <summary>シェルを起動済みかどうか。</summary>
    bool IsStarted { get; }

    /// <summary>起動したシェルが終了済みかどうか。</summary>
    bool HasExited { get; }

    /// <summary>シェルの出力。バックグラウンドスレッドから通知される。</summary>
    event EventHandler<string>? OutputReceived;

    /// <summary>シェルのプロセスが終了した。バックグラウンドスレッドから通知される。</summary>
    event EventHandler? Exited;

    /// <summary><see cref="Submit"/> が呼ばれた。端末の表示側が貼り付けとして入力する。</summary>
    event EventHandler<string>? SubmitRequested;

    /// <summary>指定の端末サイズでシェルを起動する。</summary>
    void Start(int columns, int rows);

    /// <summary>現在のシェルを終了して、同じ設定で起動し直す。</summary>
    void Restart(int columns, int rows);

    /// <summary>シェルへ入力をそのまま送る（キー入力など）。</summary>
    void Write(string text);

    /// <summary>
    /// テキストを貼り付けとして入力し、続けて Enter で確定する。
    /// 端末の表示側を通すことで、複数行でも CLI が 1 行ずつ実行せずひとまとまりで受け取れる（ブラケットペースト）。
    /// </summary>
    void Submit(string text);

    /// <summary>端末サイズを変更する。</summary>
    void Resize(int columns, int rows);
}
