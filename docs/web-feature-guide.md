# Web 版の機能の足し方

Web 版 (MmmToolWeb)に機能を足すときの、プロジェクトの作り方と、共通の部品の使い方。  
プロジェクトの一覧と役割は [specs/web.md](specs/web.md) の「構成」、画面の決まりは同じファイルの「ホーム画面と機能の切り替え」にある。

## フォルダとソリューション

- 機能ごとのプロジェクトは `src/Plugins` に置く
  - サーバー側は `MmmToolWeb.<機能>` で、API とサービスの登録を持つ。
  - 画面側は `MmmToolWeb.<機能>.Client` で、Razor の画面と API の型を持つ。
  - 機能に共通の差し込み口は、サーバー側が `MmmToolWeb.Features`、画面側が `MmmToolWeb.Features.Client`。
- MmmTool と共有するのは、`src/Data/`・`src/Users/`・各機能の `.Core`・`external/MmmSdk` だけ。
- 画面側のプロジェクト (`Client`・`Features.Client`・`<機能>.Client`)は AnyCPU にする
  - csproj に、`Platforms`・`Platform`・`PlatformTarget` の 3 つを AnyCPU で書き、`Directory.Build.props` の x64 から外す。
  - `MmmToolWeb.slnx` には、`<Platform Solution="*|x64" Project="Any CPU" />` で登録する。
- 外部への送信 (通知など)は、`MmmToolWeb.Server` に入れず、`MmmBatch` の機能として作る。

## 機能を足す手順

1. サーバー側の `MmmToolWeb.<機能>` に、`IWebFeature` の実装を作る。
   - `ClientAssembly` は、画面側のアセンブリを返す。
   - `AddServices` に、サービスを登録する。
   - `MapEndpoints(RouteGroupBuilder api)` に、`/api/<機能>` の API を足す。
2. 画面側の `MmmToolWeb.<機能>.Client` に、Razor の画面・API の型・JSON の設定・API を呼ぶ部品・登録の拡張メソッドを作る。
   - JSON の設定は、機能ごとの `JsonSerializerContext` にして、共通の `ErrorResponse` も含める。
   - API を呼ぶ部品は、共通の `ApiCaller.SendAsync` に、読み方・`ErrorResponse` の設定・見つからないときの既定の文を渡す。失敗の種類 (`ApiFailure`)と画面に出す文は、ここで決まる。
   - 登録の拡張メソッド `Add<機能>Client` で、`ClientFeature(名前, アイコンの path, 最初の画面の道, アセンブリ)` を足す。
   - 最初の画面の道は、`@attribute [Route(Route)]` と定数で 1 か所にする。
3. 本体に、プロジェクトの参照と、登録の 1 行ずつを足す。
   - サーバー: `Server` の `Program.cs` に `AddWebFeature<…WebFeature>()`
   - 画面: `Client` の `Program.cs` に `Add<機能>Client()`
4. `MmmToolWeb.slnx` と `MmmToolAll.slnx` に、足したプロジェクトを足す。
5. DB の表を読み書きするときは、Web 版の DB のログインに、その表の権限を足す。

- 画面と API を持たず、ほかの機能に提供元を登録するだけの機能は、サーバー側の 1 つのプロジェクトだけで作る。
  - 例: `MmmToolWeb.Reminders`・`MmmToolWeb.WorkItems`
  - `ClientAssembly` は、そのプロジェクト自身のアセンブリを返す。
  - 画面側の手順 (2 と、3 の画面の行)は行わない

## 共通の部品の使い方

- サーバー側
  - 機能の API は、`MapWebFeatures` が `/api` の下にまとめ、ログイン必須にして渡す。機能側で認証を付け直さない。
  - 今のユーザーは、要求ごとに作る `CurrentUser` で持つ。保存先と、それを使うサービスは Scoped で登録し、Singleton に `CurrentUser` を持たせない。
  - ほかの人が書いた文字を HTML にして返すときは、`NoteBodyFormatter` と同じ、組み立て直す方式の整形を通す。
- 画面側
  - ブラウザの機能は、`BrowserClient` と `wwwroot/js/browser.js` を通す。機能ごとに JavaScript を足さない。
    - 今日の日付: `GetTodayAsync`
    - 前面への復帰: `StartWatchingVisibilityAsync` と `BecameVisible`
    - 端末の記憶 (`localStorage`): `GetRememberedAsync` と `RememberAsync`。名前は、機能の名前から始める。
    - 最初から文が入っている入力欄のカーソルを先頭に置く: `PlaceCaretAtStartAsync` (`browser.js` の `placeCaretAtStart`)
  - 機能の画面の上のバーは `FeatureHeader` で、ホームへ戻る・名前・操作を持つ。
    - 読み込みや送信を待つ間は、`Busy` に true を渡して、進行バーを出す。
    - 速く終わっても見える最低の時間 (0.4 秒)は、`FeatureHeader` が持つ。
  - ホームは `/home` で、ログインのあとは常にここへ移る。ログアウトは `SessionClient`。
  - 色は、`app.css` の名前つきの色 (`--bg`・`--surface`・`--text`・`--muted`・`--accent`・`--accent-text`・`--border`・`--danger`・`--disabled`)を使う。
    - 押せない部品の文字は `--disabled`
