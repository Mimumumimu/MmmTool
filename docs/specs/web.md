# Web 版 (MmmToolWeb)

複数人が iPhone のホーム画面のアプリから使う Web 版の仕様。

## 概要

- 使える機能はノートとリマインダー
- ログインはパスキーだけ
- サーバーは 1 台の PC で動かし、Tailscale Funnel で公開する
- 残りの作業は [todo.md](../todo.md) の「Web 版」

## 構成

| プロジェクト | 役割 |
| --- | --- |
| `src/App/MmmToolWeb/Server` (`MmmToolWeb.Server`) | 常駐する EXE。画面のファイルを配り、API (`/api/...`)を受ける。DI 登録・API の入口・サーバーの PC で行う操作 (`Commands/`)・DB のエラーの受け止め (`Hosting/`)だけを持つ |
| `src/App/MmmToolWeb/Client` (`MmmToolWeb.Client`) | iPhone のブラウザで動く画面 (Blazor WebAssembly)で、サーバーの API だけを呼び、DB には触れない。サーバーと共有する要求・応答の型もここに置く (サーバーが参照する) |
| `src/Auth/MmmToolWeb.Auth.Core` | パスキーの登録・ログインの手続き (`PasskeyService`)・途中の状態の置き場 (`PasskeyCeremonyStore`)・パスキーの型と保存先の口。画面に依存しない |
| `src/Data/MmmToolWeb.Data.SqlServer` | SQL Server だけの、認証用の表の保存。`MmmBatch.Data.SqlServer` と同じ作りで、`MmmTool.Data.SqlServer` を参照する |

- 発行すると、Client の出力が Server の出力の中にコピーされ、1 つのフォルダになる (`dotnet publish` で `Server` を発行する。`Server` が `Client` を参照しているため、向きは Client から Server)
  - フォルダ直下 (`MmmToolWeb.Server.exe` と DLL): サーバーの PC で動く部分
  - `wwwroot\` (`index.html`・`css`・`js`・`_framework`): Client の出力。サーバーが iPhone に配るファイルで、サーバーでは実行されない
  - 1 つのフォルダにするのは、アドレスを 1 つにするため (パスキーはアドレスに結びつく)。Client を別の置き場に分けない
- ソリューションは `MmmToolWeb.slnx`。`MmmTool.slnx` からは参照せず、`MmmToolWeb.*` のプロジェクトを `MmmTool.*` が参照することもない。
- 機能 (ノート・リマインダー)の画面は、機能ごとにプロジェクトを足す。`Server` は機能の名前を持たず、登録の 1 行で足す。
- `Client` だけ AnyCPU にする (ブラウザの `browser-wasm` と x64 の指定が両立しないため)
  - ほかは x64
- 画面を Blazor WebAssembly にする
  - サーバーで描画する方式にしない (iPhone でアプリが裏に回ると、サーバーとの接続が切れて状態を失うため)

## ログイン

- パスワードを持たない。パスキー (Face ID など)で本人を確かめる。
- **登録**: 管理者が出した登録用コードを入れ、ホーム画面のアプリの中でパスキーを作る
  - 登録・ログインは、ホーム画面から開いた画面の中で行う (Safari で開いた画面とは、ログインの記録が別になることがあるため)
- **ログイン**: ユーザー名を入力させず、端末のパスキーを選ぶ方式
  - ユーザーを特定するのは、パスキーが持つ本人の印
  - 本人確認 (Face ID など)を必須にする
- **ログイン状態**: 署名つきの Cookie (名前 `mmmtoolweb.session`)
  - 有効期間は 90 日で、使うたびに延びる
  - `HttpOnly`・`Secure`・`SameSite=Lax`
- **登録用コード**: 8 桁の数字。期限は 30 分で、1 回だけ使える。総当たりは、期限と回数制限で防ぐ。
  - 管理者が、サーバーの PC で人ごとに出す (`issue-code`)。同じ人に出し直すと、前のコードは使えなくなる
  - 使えるのは、今日使える状態のユーザー (削除されておらず、使える期間の内)だけ
- **手続きの途中の状態**: 最初の問い合わせで作った選択肢を、メモリに 5 分だけ置く
  - 完了の呼び出しで 1 回取り出すと消える (同じ呼び出しの再送を通さないため)
- **回数制限**: 登録・ログインの 4 つの口は、接続元ごとに、1 分に 20 回まで
  - 接続元は、IPv6 のときは前半の 64 ビットで 1 つとみなす (端末が、回線の範囲の中の別のアドレスへ変えられるため)
  - 登録の 2 つの口 (`enroll/start`・`enroll/finish`)は、接続元を問わない全体でも、1 分に 20 回まで。接続元を偽られても、多数の接続元から試されても、登録用コードの総当たりの回数に上限を残すため
  - 全員で 1 つの枠を共有しない (スキャナーが連打しただけで、本人のログインも止まるため)
  - 数えないもの: ログイン中かの確認・ログアウト・画面のファイル
- **接続元**: サーバーは `localhost` だけで待ち受け、Funnel が同じ PC から転送してくる
  - 要求が `localhost` から来たときだけ、`X-Forwarded-For` の最後の 1 つ (転送した側が足した値)を、本当の接続元として使う
  - 手前の値は信用しない (外から偽の値を付けられるため)
- パスキーは、アドレス (ドメイン名)に結びつく。公開アドレスを替えたときは、全員が登録し直す。
- メールアドレスの列を足さない。メールでの再設定も作らない。
  - 使う場面が無い
  - 端末をなくしたときは、管理者が新しい登録用コードを出す
  - メールなどの外部送信は MmmBatch の役目で、Web 版には入れない
- パスワードを Web 版に作らない (総当たりと、偽の画面での盗み取りの的になるため)
- 標準の Identity を使わず、パスキーの検証には NuGet の `Fido2` を使う (Identity は EF Core の専用のユーザーの表を前提にし、`AppUser` とは別にユーザーの表ができるため)
- 端末ごとのログインの無効化を作らない (端末の表が増えるため)
  - 人ごとに「ログインの世代の番号」を上げて、その人の Cookie を全部無効にする

## API

| 経路 | 内容 |
| --- | --- |
| `GET /api/auth/me` | ログイン中のユーザーの名前とログインした日時を返す。未ログインは 401 |
| `POST /api/auth/enroll/start` | 登録用コードを確かめて、パスキーの登録を始める。コードが違えば 403 |
| `POST /api/auth/enroll/finish` | コードを使い終わりにして登録を完了し、ログインさせる |
| `POST /api/auth/login/start` | パスキーでのログインを始める |
| `POST /api/auth/login/finish` | ログインを完了する。失敗は 401 |
| `POST /api/auth/logout` | ログイン状態を捨てる (204) |

- 登録・ログインの口は、DB の設定が足りない・接続できないとき 503 を返す
- ログインの完了と登録の完了は、その人が今は使えない状態 (削除済み・使える期間の外)なら 401

- 登録・ログインの 4 つは、接続元ごとの回数制限の対象。登録の 2 つは、全体の回数制限の対象でもある

## DB の表 (設計)

- どちらも SQL Server だけ
- `AppUser` は変えない
- 作成 SQL は `MmmToolWeb.Data.SqlServer` の `Sql/001_CreateTables.sql` にまとめる
- 各表は、上から主キー・共通ヘッダー・中身の列の順に並ぶ
- 共通ヘッダーの `CreatedAt`・`CreatedByUserId`・`UpdatedByUserId` は、検索の条件・結合・索引に使わない ([database.md](database.md))

## `AppUserPasskey` (パスキー)

- 1 人が登録した 1 つのパスキー
- 1 人が複数持てる

| 列 | 型 | SQL Server | 既定値 | 内容 |
| --- | --- | --- | --- | --- |
| `Id` | 自動採番の整数 | `int IDENTITY(1,1)` | | 主キー |
| `IsDeleted` | 真偽 | `bit` | 0 | 論理削除 (端末をなくしたときに、そのパスキーを無効にする) |
| `CreatedAt` | 日時 (時差つき・ミリ秒) | `datetimeoffset(3)` | 現在 | 作成日時 |
| `CreatedByUserId` | 整数 | `int` | | 作成者 (`AppUser.Id`)で、0 以上の検査 |
| `UpdatedAt` | 日時 (時差つき・ミリ秒) | `datetimeoffset(3)` | 現在 | 更新日時 |
| `UpdatedByUserId` | 整数 | `int` | | 更新者 (`AppUser.Id`)で、0 以上の検査 |
| `AppUserId` | 整数 | `int` | | 持ち主 (`AppUser.Id`)で、外部キーは付けない |
| `CredentialId` | バイト列 | `varbinary(1024)` | | パスキーの識別番号で、認証器が決める。削除されていない行の間だけ一意 (`UX_AppUserPasskey_CredentialId`) |
| `PublicKey` | バイト列 | `varbinary(1024)` | | 公開鍵。署名の検証に使う |
| `SignCount` | 整数 | `bigint` | 0 | 認証器が数えた使用回数で、32 ビットの符号なしを入れるので `bigint`。前回より増えていないときは複製を疑う |
| `UserHandle` | バイト列 | `varbinary(64)` | | パスキーが持つ本人の印。ユーザー名とは別のランダムな値で、同じ人の行はすべて同じ値 |
| `DeviceName` | 文字列 (50) | `nvarchar(50)` | 空文字 | 端末の呼び名 (「iPhone」など) |
| `LastUsedAt` | 日時 (時差つき・ミリ秒) | `datetimeoffset(3)` | 現在 | 最後にログインに使った日時 |

- 索引は、次の 2 つ
  - `CredentialId` の一意: ログインのとき、識別番号から探す
  - `AppUserId`: 人ごとの一覧
- 作成者・更新者は、パスキーの持ち主 (`AppUserId`)。本人が登録し、本人がログインで更新するため

## `AppUserWebAuth` (Web 版の認証の状態)

- 1 人に 1 行で、登録用コードと、ログインの世代の番号を持つ
- 行が無い人は、コードが無く、世代は 1 として扱う
- 行は、管理者が最初にコードを出すときに作る

| 列 | 型 | SQL Server | 既定値 | 内容 |
| --- | --- | --- | --- | --- |
| `Id` | 自動採番の整数 | `int IDENTITY(1,1)` | | 主キー |
| `IsDeleted` | 真偽 | `bit` | 0 | 論理削除 |
| `CreatedAt` | 日時 (時差つき・ミリ秒) | `datetimeoffset(3)` | 現在 | 作成日時 |
| `CreatedByUserId` | 整数 | `int` | | 作成者 (`AppUser.Id`)で、0 以上の検査 |
| `UpdatedAt` | 日時 (時差つき・ミリ秒) | `datetimeoffset(3)` | 現在 | 更新日時 |
| `UpdatedByUserId` | 整数 | `int` | | 更新者 (`AppUser.Id`)で、0 以上の検査 |
| `AppUserId` | 整数 | `int` | | 対象のユーザー (`AppUser.Id`)で、外部キーは付けない。削除されていない行の間だけ一意 (`UX_AppUserWebAuth_AppUserId`) |
| `EnrollmentCodeHash` | 文字列 (64) | `varchar(64)` | 空文字 | 登録用コードの SHA-256 で、16 進。コード本体は保存しない。空文字は、有効なコードが無い状態 |
| `EnrollmentCodeExpiresAt` | 日時 (時差つき・ミリ秒) | `datetimeoffset(3)` | `9999-12-31` | 登録用コードの期限。コードが無いときは `9999-12-31` |
| `SessionVersion` | 整数 | `int` | 1 | ログインの世代の番号。Cookie に入れた番号と違う Cookie は無効。上げると、その人のログインがすべて無効になる |

- 索引は、次の 2 つ
  - `AppUserId` の一意: 1 人に 1 行
  - `EnrollmentCodeHash`: 入力された登録用コードから、持ち主を探す。コードが空の行は対象にしない
- 作成者・更新者は 0。コードを出すのは、ユーザーのログインではなく、サーバーの PC での操作のため

## DB の接続

- アプリが使うログインは、専用の `MmmToolWeb` を作る
  - 権限は、ノート・リマインダー・`AppUser`・認証用の表だけ
  - 権限を必要な表だけに絞る考え方は、`MmmBatch` に絞ったログインを作るときと同じ。[database.md](database.md)の「MmmBatch」の項を見る
- Windows 認証にしない (サーバーを動かしたユーザーが管理者権限のとき、Web アプリが破られると DB 全体を読まれるため)
- 接続のパスワードは、SDK の `ISecretStore` (実装は `CredentialSecretStore`。サーバーを動かすユーザーの資格情報マネージャーに保存する)に置く。サーバーだけが持ち、画面 (Client)は接続先もパスワードも知らない

- 開発用の DB に作る手順は、次の 3 つ
  - SSMS で `master` に対して、ログインを作る。`<パスワード>` は決めた値にする。

    ```sql
    CREATE LOGIN [MmmToolWeb] WITH PASSWORD = N'<パスワード>', CHECK_POLICY = OFF, DEFAULT_DATABASE = [MmmTool];
    ```

  - `MmmTool` DB に対して、`MmmToolWeb.Data.SqlServer` の `Sql/001_CreateTables.sql` を流す。フィルター付きの索引があるため、`QUOTED_IDENTIFIER` が ON の接続で流す。`sqlcmd` なら `-I` を付ける。
  - `MmmTool` DB に対して、DB ユーザーと権限を作る。`AppUser` は `PasswordHash` を除く列だけ読める。DELETE は付けない。

    ```sql
    CREATE USER [MmmToolWeb] FOR LOGIN [MmmToolWeb];
    GRANT SELECT, INSERT, UPDATE ON dbo.AppUserPasskey TO [MmmToolWeb];
    GRANT SELECT, INSERT, UPDATE ON dbo.AppUserWebAuth TO [MmmToolWeb];
    -- AppUser の列のうち、PasswordHash 以外を列挙して付ける
    GRANT SELECT (Id, IsDeleted, CreatedAt, CreatedByUserId, UpdatedAt, UpdatedByUserId, LoginName, DisplayName, ValidFrom, ValidTo) ON dbo.AppUser TO [MmmToolWeb];
    ```

  - ノートとリマインダーの表の権限は、それぞれの Web 画面を作るときに足す

## 動かし方

- 起動は `.\MmmToolWeb.Server.exe --urls http://localhost:5080 --Web:PublicOrigin=<公開アドレス>`
  - `Web:PublicOrigin` は、外から見えるアドレスで、パスキーの検証に使う (既定は `http://localhost:5080`)
- 公開は `tailscale funnel 5080`。サーバーは `localhost` だけで待ち受け、外へ出すのは Funnel だけにする。
- 起動と停止は手動 (サービスやスタートアップにしない)
- 配る形は、`dotnet publish` の出力 (フレームワーク依存)
  - 動かす PC に ASP.NET Core Runtime 10 が要る

## サーバーの PC で行う操作

画面を持たないので、設定と管理は、サーバーの PC でコマンドとして実行する。実行すると、サーバーは起動せず、終わったら閉じる。

| コマンド | 内容 |
| --- | --- |
| `.\MmmToolWeb.Server.exe setup-db` | DB の接続の設定を聞く (サーバー名・データベース名・ユーザー名・パスワード・証明書を信頼するか)。接続を試して、通ったときだけ保存する。パスワードは画面に出さない。Windows 認証は選べない |
| `.\MmmToolWeb.Server.exe issue-code <ログイン名>` | その人の登録用コードを出す。前に出したコードは使えなくなる |

## 探りの遮断

存在しない道を順に試す自動の探り (`.env` や `/.git/config` など)を繰り返す接続元を、一定時間断る。

- 探りに数える要求は、次の 2 つ
  - `404` で終わった要求。ただし、普通に使っても出る 3 つの道 (`/favicon.ico`・`/apple-touch-icon-precomposed.png`・`/MmmToolWeb.Client.styles.css`)は数えない
  - 画面の入口 (`index.html`)を返しただけの要求のうち、画面の道ではないもの。拡張子のない道には画面の入口を返す作りのため、`/.git/config` や `/debug/` が `200` で終わるので、これで見つける
- 画面の道は、Client の画面の `@page` から、起動時に集める。画面を足しても、サーバー側は直さない。
- 接続元ごとに、1 分に 10 回の探りで、その接続元を 30 分のあいだ、すべての道で `429` にして断る
  - 断った要求も、アクセスログには残る
  - 数える接続元は 10 万件まで。それを超えた接続元は、数えない (メモリを際限なく増やされないため)
- 断った接続元の一覧を、`Data/Logs/rejected-clients.txt` に残す
  - 載せるのは、探りで断った接続元と、登録・ログインの回数制限で断った接続元
  - 1 行が 1 つの接続元で、タブ区切り。接続元・最初に断った日時・最後に断った日時・断った回数・最後の道を書く
  - 最後に断った日時の新しい順で、変わったときだけ 1 分ごとに書き直す。止めるときにも書く。起動し直しても、続きから数える
  - 載せる接続元は 1 万件まで (ファイルを際限なく大きくされないため)
- 断り始めたときに、`server-*.log` に 1 行書く
- 断る基準 (1 分に 10 回・30 分)を、設定で変えられるようにしない (変える必要が出たときに、コードの定数を直す)

## ログ

`Data/Logs` に、3 種類のファイルを書く。窓 (コンソール)には、警告以上だけを、1 件 1 行で、先頭に日時を付けて出す。

| ファイル | 内容 |
| --- | --- |
| `access-*.txt` | W3C 形式のアクセスログ。アクセスのたびに 1 行で、日付・時刻・接続元・メソッド・道・状態・かかった時間・ユーザーエージェント・`X-Forwarded-For`・`X-Original-For` を書く |
| `server-yyyy-MM-dd.log` | 警告以上のログと、守りに関わる出来事 |
| `rejected-clients.txt` | 断った接続元の一覧。内容は「探りの遮断」を見る |

- アクセスログは、ファイルの大きさと数に上限がある (既定の大きさで、60 ファイルまで)
  - Cookie は書かない (ログインの値が、そのまま残るため)
  - 存在しない道への探りも、ここに残る
- `server-*.log` に書く、守りに関わる出来事
  - ログイン状態の発行は、登録の完了とログインの完了のとき。ユーザー番号と接続元を書く
  - 登録・ログインの口の失敗と拒否は、状態が 400 以上のとき
    - 回数制限の 429 は書かない。連打されると際限なく増えるため。アクセスログには残る
  - 登録用コードの発行は、`issue-code` のとき。ユーザー番号を書く
- `server-*.log` は、1 日 10 MB に達したら、その日の記録を止める。大量の拒否でディスクを埋められないようにするため。
- `server-*.log` は、自動では消えない。

## 保存するもの

実行ファイルのフォルダの `Data` に保存する。

| 場所 | 内容 |
| --- | --- |
| `Data/AppSettings.json` の `Database.*` | サーバー名・データベース名・ユーザー名・ログインの方式 (SQL 認証)・証明書を信頼するか。MmmTool と同じキー |
| 資格情報マネージャーの `MmmToolWeb.Database` | DB の接続のパスワード |
| `Data/Logs/access-*.txt` | アクセスログ。内容は「ログ」を見る |
| `Data/Logs/server-yyyy-MM-dd.log` | 警告以上のログと、守りに関わる出来事。内容は「ログ」を見る |
| `Data/Logs/rejected-clients.txt` | 断った接続元の一覧。内容は「探りの遮断」を見る |
| `Data/Keys` | Cookie の暗号化の鍵。ASP.NET Core の Data Protection が作り、Windows のユーザーに結びつけて暗号化する。アプリ名を `MmmToolWeb` に固定する。発行先を替えるときは、`Data` を持ち越し、同じ Windows ユーザーで動かせば、ログインは切れない |
