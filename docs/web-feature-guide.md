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
  - 例: `MmmToolWeb.WorkItems`
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
  - 日付を入れる欄は、`<input type="date">` ではなく `DatePicker` を使う。仕様は [specs/web.md](specs/web.md) の「日付の選択 (Web 版)」。
    - 渡すのは次の 4 つ。
      - `Value`: 選んでいる日で、未設定は null
      - `OnPicked`: 日を選んだときの処理
      - `Label`: 欄の名前
      - `Invalid`: true にすると、誤りの欄として太い枠にする
    - `<label>` で包まない。カレンダーの空きをタップすると、欄を押したことになって、開き直すため。見出しの文字は `<span>` で出す。
    - 土曜・日曜・祝日の線に使う祝日は、`HolidayClient` が、サーバーの `GET /api/holidays` から読む。部品が使うので、機能の側では呼ばない。
  - ログインが切れたとき (API が 401)は、共通の呼び出しの部品 `ApiCaller` が、`SessionClient.GoToSignIn` でログインの画面へ全体を読み直して戻す。
    - 機能の API を呼ぶ部品 (`<機能>Client`)は、`SessionClient` を受け取り、`ApiCaller.SendAsync` の最後の引数に `session.GoToSignIn` を渡す。
    - 画面は、移動を書かない。`ApiFailure.SignedOut` のときは、失敗の文を出さずに処理を終える。
  - PC 版と共有する決まり (送れる条件・文字数や人数の上限・状態の値・曜日)は、画面側が、機能の `.Core` を参照して、そのまま使う。画面側のコードに、数字や式を書き写さない。
    - 参照できるのは、Messages・Notes・Reminders の AnyCPU の `.Core`。x64 のままの `.Core` は、ブラウザで動かないので、参照できない。
    - 画面は、上限を、API の値ではなく、共通の定数から読む。
    - API が返している上限の項目は、消さない。更新の前に開いたままの画面が、その値から読んで動くため。
    - 参照を足すときは、その `.Core` と、参照先の `.Core` を、すべて AnyCPU にする。方法は [build-and-distribution.md](build-and-distribution.md) の「ビルドの共通設定」。
  - 日付を API の要求や通知から開く道に載せる文字 (`yyyy-MM-dd`)は、`ApiDay` で作る・読む。書式を、画面やサーバーに書き写さない。
  - 機能の画面の上のバーは `FeatureHeader` で、ホームへ戻る・名前・操作を持つ。
    - 読み込みや送信を待つ間は、`Busy` に true を渡して、進行バーを出す。
    - 速く終わっても見える最低の時間 (0.4 秒)は、`FeatureHeader` が持つ。
  - ホームは `/home` で、ログインのあとは常にここへ移る。ログアウトは `SessionClient`。
  - 色は、`app.css` の名前つきの色 (`--bg`・`--surface`・`--text`・`--muted`・`--accent`・`--accent-text`・`--border`・`--danger`・`--disabled`・`--danger-fill`・`--danger-fill-text`・`--saturday`・`--sunday`)を使う。
    - 押せない部品の文字は `--disabled`
    - 色の値は、`app.css` の `:root` に、ライトとダークの両方を 1 回だけ書く。画面の CSS に、同じ意味の色を書き写さない。
  - 共通のクラスは、`MmmToolWeb.Features.Client` の `wwwroot/css/features.css` にある。画面の `<画面>.razor.css` には、その画面だけの見た目と、余白の違いだけを書く。同じ宣言を書き写さない。
    - 共通のクラスは、機能を足しても増やさず、そのまま使う。足りないときは、`features.css` に足して、全部の画面で使えるようにする。
    - 枠: `.page` (ホーム・ログイン)・`.screen` (機能の画面)
      - 右下に `.fab` があるときは、`.screen` に `with-fab` を足す。
      - 編集のシートが開いている間は、`.screen` に `behind` を足す。
    - 押せる部品: `.text` (文字だけのボタン)・`.fab` (右下に浮かべる主な操作)・`.check` と `.checks` (チェックの行と、横に並べた行)・`.segments` と `.segment` (横に並べた選択)
      - `.text` には、`.strong` と `.danger` を足せる。
    - 入力の欄: `.form-field` (欄の名前の `span` と入力欄)・`.form-group` (`fieldset`)・`.form-label` (入力欄から離れて置く名前)
    - 文字と帯: `.hint` (欄の下の補足)・`.muted`・`.caption` (薄い小さな見出し)・`.empty` (何もないとき)・`.message` (失敗の文)・`.info` (知らせの帯)・`.result` と `.result.error` (操作の結果の帯)
    - 一覧: `.list`・`.card` (1 件の枠)
      - `.card` には、`.selected`・`.finished`・`.deleted` を足せる。
    - 幕と箱: `.backdrop` (`.center`・`.bottom`・`.front`)・`.bottom-panel`
    - 同じ名前で意味の違うクラスを、画面の CSS に作らない。`.field`・`.hint` を別の意味で使って、共通のクラスと混ざった前例がある。
  - 構造のある部品は、`Features.Client` の部品を使う。
    - `FeatureHeader` (上のバー)・`SheetFrame` (全画面のシート)・`EditSheet` (入力のシート)・`ConfirmDialog` (取り返しにくい操作の確認)・`Chevron` (山形)・`DatePicker`
      - `SheetFrame` の上のバーは、`FeatureHeader` を使う。
      - `EditSheet` は、左にキャンセル・右に確定を置き、キーボードを出すと高さが縮む。
    - 確認のダイアログや、山形の SVG を、画面の側に作らない。
