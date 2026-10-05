# 0002 起動時の準備は `IStartupTask` にする (`IHostedService` にしない)

## 状況
起動時に、CLI補助の利用状態の読み込み・リマインダー監視の開始・リンクの先読みなどを行いたい。これらは UI スレッド (`DispatcherQueue`)で動く必要がある。

## 決定
アプリ側に `IStartupTask { Task StartAsync(); }` を用意し、`App.OnLaunched` で、`TrayIcon` を解決したあと・`MainWindow` を作る前に、UI スレッドで登録順に `await` する。各機能が `AddStartupTask<T>()` で登録する。

## 理由
Generic Host の `IHostedService` は、Host が内部で `ConfigureAwait(false)` を使うため、UI スレッドで動く保証がない。

## 影響
起動時の準備で起動を止めたくない例外 (読み込みの失敗など)は、`<機能>Startup` の中で受け止め、画面を開いたときに知らせる。
