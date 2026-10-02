namespace MmmTool.Services.Terminal;

/// <summary>
/// 擬似コンソール上で動くシェル 1 つ分のセッション。
/// </summary>
public interface ITerminalSession : IDisposable
{
    /// <summary>起動するコマンドライン</summary>
    /// <remarks>Start 前に設定する。</remarks>
    string CommandLine { get; set; }

    /// <summary>起動時の作業ディレクトリ</summary>
    /// <remarks>Start 前に設定する。</remarks>
    string WorkingDirectory { get; set; }

    /// <summary>シェルを起動済みかどうか。</summary>
    bool IsStarted { get; }

    /// <summary>起動したシェルが終了済みかどうか。</summary>
    bool HasExited { get; }

    /// <summary>シェルの出力</summary>
    /// <remarks>バックグラウンドスレッドから通知される。</remarks>
    event EventHandler<string>? OutputReceived;

    /// <summary>シェルのプロセスが終了した</summary>
    /// <remarks>バックグラウンドスレッドから通知される。</remarks>
    event EventHandler? Exited;

    /// <summary><see cref="Submit"/> が呼ばれた</summary>
    /// <remarks>端末の表示側が貼り付けとして入力する。</remarks>
    event EventHandler<string>? SubmitRequested;

    /// <summary>指定の端末サイズでシェルを起動する。</summary>
    void Start(int columns, int rows);

    /// <summary>現在のシェルを終了して、同じ設定で起動し直す。</summary>
    void Restart(int columns, int rows);

    /// <summary>シェルへ入力をそのまま送る（キー入力など）。</summary>
    void Write(string text);

    /// <summary>テキストを貼り付けとして入力し、続けて Enter で確定する。</summary>
    /// <remarks>端末の表示側を通すことで、複数行でも CLI が 1 行ずつ実行せずひとまとまりで受け取れる（ブラケットペースト）。</remarks>
    void Submit(string text);

    /// <summary>端末サイズを変更する。</summary>
    void Resize(int columns, int rows);
}
