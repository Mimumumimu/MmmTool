# ビルドと配布

アプリのビルドの共通設定と、配布物の作り方と、その決定の理由。全体構成は [architecture.md](architecture.md)。

## ビルドの共通設定
- リポジトリ直下に `Directory.Build.props`(バージョン・Nullable・ImplicitUsings・`GenerateDocumentationFile`・`EnforceCodeStyleInBuild`・全プロジェクトを x64 専用にする (`Platforms` と既定の `Platform`。Core も含めて AnyCPU を使わない。`-p:Platform` を付けずにビルド・発行しても x64 になる。SDK も同じ設定)・XML ファイルをビルドの出力・発行物に含めない・Release では .pdb を作らない)、`Directory.Packages.props`(中央パッケージ管理。csproj の `PackageReference` にはバージョンを書かない)、`.editorconfig`(`root = true`)を置く。3 つとも slnx の「Solution Items」に入れている
- `.editorconfig`: 未使用 using・ファイル単位の名前空間・using の位置・複数行の本体の波かっこを warning にしてビルドで検査する。1 行の早期 return (`if (x) return;`)は波かっこを省略してよい。`charset` は書かない (BOM 付きの ps1 があるため)
- SDK はリポジトリ直下に自分の同じ 1 組を持つ。MSBuild・.editorconfig は近いほうを使うので、アプリと SDK の設定は混ざらない。共通のパッケージ (CommunityToolkit.Mvvm・Windows App SDK (SDK は部品のパッケージ、アプリは全部入り)・SDK.BuildTools)は SDK を先に上げて、アプリを同じバージョンにする
- バージョンは `Version`(ファイル・アセンブリのバージョンは自動で末尾に 0 を足した 4 桁)。製品バージョンの後ろにはコミット番号が付く (.NET の標準の動作)
  - 変更履歴は、配布用の説明書 `src/App/MmmTool/Distribution/README.txt` の「変更履歴」に書く (Keep a Changelog の形)。利用者から見える変更 (機能の追加・変更・不具合の修正)をしたら、同じ作業の中で、先頭の「未リリース」の見出しの下に 1 行ずつ書き足す (見出しが無ければ作る)
  - 次の版の番号は、公開するとき (ユーザーが決めたとき)に、「未リリース」に溜まった中身から決める (セマンティックバージョニング。機能の追加は真ん中、不具合の修正だけなら最後を上げる。1.0.0 未満のあいだは、大きな変更でも真ん中を上げる)。公開するときに、「未リリース」を「<版>(<日付>)」に書き換え、`Version` も同じ値にする。SDK の版は別に持つ
  - ライセンスは MIT (`LICENSE.txt`。著作者 mimumu。SDK も同じ)。ライセンスの文書 (`LICENSE.txt`・自動で作る同梱ライブラリのライセンス全文 `THIRD-PARTY-NOTICES.txt`。手で直さない)は、アプリ本体の `Assets\Licenses\` に入る (「配布」)
- XML コメントの検査と未使用 using の検査は、普通の `dotnet build` / VS のビルドでかかる

## 配布
- フレームワーク依存 (`SelfContained=false` / `WindowsAppSDKSelfContained=false`)。実行する PC に .NET 10 Desktop Runtime と Windows App Runtime 2.5 が必要。決めた理由は下の「決定の理由」の「フレームワーク依存で配布する」
- 発行は VS の「発行」 (プロファイル `win-x64`)、またはコマンドの `dotnet publish .\src\App\MmmTool\MmmTool.csproj -p:PublishProfile=win-x64`。どちらも、配布物は `src/App/MmmTool/bin/Release/publish/MmmTool_<版>/` にでき、アプリ本体と説明書は、その中に並ぶ(版は `Directory.Build.props` の `Version`。通常のビルドの出力 `bin/Release/net10.0-windows10.0.19041.0/` の隣。対象の .NET・RID は 1 つずつなので、出力先の名前に入れない)。
  - 2 段階にしている: 発行 (`PublishDir`)は、版を含まない途中のフォルダ `obj/Release/publish/` に出し、発行のあとに csproj の `PackageDistribution` が、アプリ本体と説明書類を `MmmTool_<版>/` の直下に並べる (配布物の場所は `DistributionDir`。試すときは `-p:DistributionDir=...` で変えられる)
  - 版をプロファイルの出力先に書かない理由: VS の「発行」は、発行プロファイルを単体で読んで出力先を決めるので、プロジェクトの `Version`(`Directory.Build.props`)が見えず、`MmmTool_\` になる。プロファイルで `Directory.Build.props` を `Import` すると、VS がプロファイルを読めなくなる (一覧から消える)。そのため、版はプロジェクトの中 (`PackageDistribution`)で付ける
- 別の PC で、クローン済みのフォルダを今のブランチの最新にして発行するには、`scripts/update_and_publish.bat` を実行する (リポジトリの根元は、バッチの 1 つ上のフォルダ。どこにクローンしても動く)。`git pull --ff-only` → `git submodule update --init --recursive` → 上の `dotnet publish` の順に実行し、どれかが失敗した時点で止まる。出力先の考え方は上と同じで、発行の前に既存のファイルは消さない。理由は下の「決定の理由」の「更新と発行のバッチをリポジトリに入れる」
- 版のフォルダ (`MmmTool_<版>`)をそのままコピーして配布する。版のフォルダの直下に、アプリ本体と説明書を並べる形はユーザーの決定 (2026-10-12。一般的な配布物の形で、`MmmTool.exe` の横に説明書が見える。入れ替えで中身を上書きすれば説明書も新しくなり、フォルダを移すと説明書もついてくる。以前は、版のフォルダの中に `MmmTool/` を持つ二段の形だった)。版ごとにフォルダが分かれるので、前の版の控えも残る。フォルダ名に版が入るため、版が変わるとショートカットは張り直しになるが、入れ替えの方法 (上書き・新しいフォルダ・フォルダ名の付け替え)は利用者の自由にする。`bin` の下にあるので、`bin` を丸ごと消すと一緒に消える点に注意する。発行の前に既存のファイルを消す設定 (`DeleteExistingFiles`)は使わない (ユーザーの決定)
- ビルドの出力先は `bin\<構成>\<TFM>\`、中間ファイルは `obj\<構成>\<TFM>\`(`Directory.Build.props` の `AppendPlatformToOutputPath=false`・`AppendRuntimeIdentifierToOutputPath=false`。プラットフォーム・RID は x64・win-x64 だけなので、段を作らない。SDK も同じ)
  - RID の段を作らない理由: VS の「発行」は、発行プロファイルの RID を参照先 (SDK)にも渡し、参照先はビルドし直さない。段を作ると、発行のときだけ `...\win-x64\MmmSdk.WinUI.dll` を探しに行き、先にビルドした DLL (段なし)を見つけられずに失敗する
- 発行は Release でだけ行う (csproj の `EnsureReleaseForPublish`)。構成は、全プロジェクトに渡る形 (VS の「発行」・`dotnet publish`・`-p:Configuration=Release`)で決める。発行プロファイルの `Configuration` だけに頼る呼び方 (MSBuild を直接呼ぶなど)では、構成がこのプロジェクトにしか効かず、参照先が Debug でビルドされて配布物に入る。`Directory.Build.props` の Release の設定 (`.pdb` を作らない)も効かない (プロファイルは `Directory.Build.props` より後に読まれる)。そのため、そのときはエラーで止める
- 発行プロファイルは `src/App/MmmTool/Properties/PublishProfiles/win-x64.pubxml` の 1 つだけで、リポジトリに入れる (`.gitignore` の `*.pubxml` から、このファイルだけを外している。フォルダーへの発行なので秘密の情報を含まない)。プロファイルに書くのは構成 (Release)・プラットフォーム・RID・出力先 (版を含まない `obj\Release\publish\`)のプロパティだけで (VS は `Import` などを書いたプロファイルを読めない)、コマンドの発行と同じ結果になるようにする。発行の設定の本体 (ReadyToRun・トリミングなし・フレームワーク依存)は csproj の `Publish Properties` などに書き、プロファイルに重ねて書かない。MSIX 用のマニフェスト・ロゴは持たない (非パッケージで配布する)。`EnableMsixTooling` は、非パッケージでも WinUI のリソース生成に使うため true のまま残している
- 配布物の版のフォルダには、アプリ本体と並べて、配布用の説明書 `README.txt`(元は `src/App/MmmTool/Distribution/README.txt`。最初に (Data フォルダ)・入れ替え・機能一覧・動作環境・置き場所・起動と終了・データの保存場所・アンインストール・変更履歴・ライセンス)と、機能ごとの使い方の `操作説明.html`(元は `src/App/MmmTool/Distribution/操作説明.html`。1 ファイルで完結させ、外部の CDN・フォントを使わない。ライト・ダークの両方で読める色にする)を置く。ライセンスの文書 (`LICENSE.txt`(リポジトリ直下のもの。MIT)・`THIRD-PARTY-NOTICES.txt`)は、`Assets\Licenses\` に入れる (EXE の横のファイルを増やさず、アプリ本体だけをコピー・再配布されても、ライセンスが一緒に付いていくようにするため。ユーザーの決定)。`THIRD-PARTY-NOTICES.txt` は、csproj の `GenerateThirdPartyNotices`(インラインタスク `GenerateThirdPartyNoticesFile`)が、発行のたびに、発行するファイルの一覧 (`ResolvedFileToPublish`)から作る (手で書くと、パッケージの追加・版の更新でずれるため)。中身は、NuGet パッケージのライセンスファイル (無ければ、MIT なら nuspec の著作権表示で本文を作る)と第三者の通知 (`NOTICE`・`ThirdPartyNotices`)、配布物の中の `名前.LICENSE.txt`(xterm.js など)。同じ本文のものは 1 つにまとめる。自分のプロジェクト (`MmmTool.Core`・MmmSdk)は含めない (`LICENSE.txt`)。説明書は csproj の `DistributionDocument` に並べ、`PackageDistribution` が発行のあとに版のフォルダへコピーする (ビルドの出力には入れない)。説明書を `MmmTool.exe` の横に置くのは、受け取った人が exe を探すときに説明書が目に入り、入れ替えやフォルダの移動でも説明書がついていくため (ユーザーの決定。2026-10-12)。`README.txt` はメモ帳で開く前提で、UTF-8 (BOM 付き)・CRLF にする (古いメモ帳でも文字化けしないように)
- リリースビルドには DEBUG ページ (コード・XAML)を含めない (csproj の条件付き `Remove`)
- WinUI の多言語リソース (言語名フォルダ内の `.mui`)は `SatelliteResourceLanguages` では消えないため、csproj の `PruneMuiAfterBuild` / `PruneMuiAfterPublish` で ja-JP・en-us 以外を削除する
- EXE の横のフォルダは `Assets`(配布物)・`Data`(アプリが書くもの)・`Lib`(パッケージの DLL)の 3 つだけにする (理由は下の「決定の理由」の「EXE の横のフォルダと DLL」)
  - `MmmTool.dll` 以外の DLL は `Lib` に置く (csproj の `MovePackageFilesToLib`・`MoveReferencesToLib`・`MoveRuntimePackFilesToLib`)。パッケージの言語別リソース (日本語の衛星アセンブリ。例: `Microsoft.Data.SqlClient.resources.dll`)も、EXE の横の言語フォルダー (`ja/`)ではなく `Lib/ja/` に置く (`MoveSatellitesToLib`)。起動時に見つけられるよう、`MmmTool.deps.json` の各ファイルに `localPath` を書き足す (インラインタスク `AddLocalPathToDepsFile`)。EXE の横に残るのは `MmmTool.*` の 5 ファイル (exe・dll・deps.json・runtimeconfig.json・pri)と `Microsoft.Web.WebView2.Core.dll`(配布物では、このほかに説明書の `README.txt`・`操作説明.html` が並ぶ)(WinUI の WebView2 が EXE のフォルダから読むネイティブの部品)
  - Release では `.pdb` を出力に入れない (`Directory.Build.props`。Debug では入れる)。`.xml`(XML ドキュメントコメント)は、Debug・Release とも出力に入れない (検査のために obj には作る)
  - Windows App SDK の使わない部品 (AI・ML・検索・ウィジェット)は出力に入れない。SDK は使う部品のパッケージだけを参照し、アプリは全部入りを参照したうえで使わない部品を `ExcludeAssets="all"` にする (CommunityToolkit が古い全部入りに依存しているため)。全部入りを上げるときは、`Directory.Packages.props` の部品の版も、新しい全部入りの依存に合わせる
  - ビルドも x64 専用 (`RuntimeIdentifier=win-x64`。出力先のパスに RID は付けない。`Directory.Build.props`)。ネイティブ DLL は `runtimes\` ではなく EXE の横に置かれる
  - 画面の XAML (`.xbf`・SDK の `.xaml`)は `MmmTool.pri` の中に入る。WinUI のビルドがばらのファイルも出力へコピーするので、csproj の `RemoveLooseXamlAfterBuild` がビルドのあとに消す (中身が `.xbf`・`.xaml` だけのフォルダと、EXE の横の `.xbf`)。発行の出力には、もともと出ない
  - WebView2 のキャッシュは `Data\WebView2`、エラーのログは `Data\Logs`(`Shell/AppInfo`)
- 同梱の xterm.js 6.0.0 / addon-fit 0.11.0 (MIT)は SDK の `MmmSdk.WinUI/Components/Terminal/Assets/`(ライセンスファイルも同じ場所)。SDK の csproj が、出力・発行フォルダーの `Assets/Terminal/` へ配る

## 版を上げるとき
版を上げるのは、ユーザーが決めたときだけ。次の順で行う (毎回同じ手順にして、やり取りを増やさない)。

1. **版の番号を決める**: 「未リリース」に溜まった中身から決める (ビルドの共通設定の規則)。決めたら、ユーザーに 1 回だけ確認する
2. **`Directory.Build.props` の `Version` を直す**: 版の数字を書く場所は、ここと配布用 `README.txt` の「変更履歴」だけ。`README.md`・docs・`操作説明.html` には、版の数字を書かない (書くと、版を上げるたびに直す場所が増える)
3. **配布用 `README.txt` の「変更履歴」を整える**: 「未リリース」を「<版> (<日付>)」に書き換え、中身を次の形に整理する (昔の版の項目は、そのまま残す)
   - 「新機能 (サイドバーに追加)」(機能名: 概要の 1 行)と、「既存機能の更新」(「追加」「変更」「修正」)に分ける
   - 文体は、体言止めで短く (「〜を追加」「〜に変更」「〜する不具合を修正」)。括弧で説明を重ねず、概要だけ書く。細かい仕様 (文字数の上限・内部の動き)は書かない
   - その版で初めて入れた機能の、リリース前の不具合の修正は、書かない (利用者は、直る前の状態を知らない)
   - サーバー保存や外部送信は、具体的な手順・構成を書かず、「サーバー保存に対応」のように、ぼかして書く。外部送信 (ntfy・Discord)は、書かない
4. **DB のスクリプトを確かめる**: 未リリースの版の DDL は、開発中に 1 本 (SQL Server と SQLite それぞれ `001_CreateTables.sql`。MmmBatch の `Sql/` も同じ)に保っているので、まとめ直す作業は要らない。リリースしたあとの変更は、次の番号の更新用スクリプト 1 本 (`002_…`)に集め、リリースまで同じファイルを直す (CLAUDE.md の「DB の変更 (SQL)は、開発中は、細かく番号つきのスクリプトに分けない」)。リリース前に、空の DB に流して確かめる (下の「空の DB での確かめ方」)
5. **配布用の文書を最新にする**: `README.txt` (機能一覧・必要なもの・保存場所など)と `操作説明.html` が、今の画面と合っているか。機能を足したときは、同じ作業で直してあるはず (CLAUDE.md の決まり)なので、版を上げるときは確認だけ
6. **ビルドと発行で確かめる**: `MmmTool.slnx` と `MmmBatch.slnx` のビルド、発行して `MmmTool_<版>\` の直下に `MmmTool.exe`・`README.txt`・`操作説明.html`・`Lib\`・`Assets\` が揃うこと
7. **コミット・PR・マージ**: CLAUDE.md の決まりに従う (ユーザーが指示したときだけ)
8. **GitHub Releases に出す**: `master` からの発行 (zip 作り)は、ユーザーが行う。ユーザーが zip の場所を示して「リリースして」と頼んだら、次を行う (この頼みが、タグの作成とリリースの公開の許可。タグの付け替え・zip の差し替えは履歴の書き換えなので、別にユーザーの許可を取る)
   - **zip を確かめる**: `<版>` として、`MmmTool_<版>/` の直下に `MmmTool.exe`・`README.txt`・`操作説明.html`・`Lib/`・`Assets/` が揃うこと、`.pdb` が無いことを、zip を開かずに一覧で読んで確かめる。zip を作ったクローンの `git log -1` が、`origin/master` の先頭と同じコミットであること (`git fetch` のあと)も確かめる
   - **本文を書く**: 一時ファイル (scratchpad)に書く。構成は、補足 (あれば)・`Data` フォルダの注意 (`README.txt` の「最初に読んでください」と同じ内容。毎回、先頭近くに入れる。理由は [specs/storage.md](specs/storage.md) の「決定の理由」)・新機能・既存機能の更新 (要点だけ。詳しくは zip の `README.txt` の「変更履歴」を見てもらう)・使い方・動作環境。外部送信 (ntfy・Discord)は書かない
   - **リリースを作る**: `gh release create v<版> <zip> --target master --title "MmmTool <版>" --notes-file <本文>`。`--target` には、**ブランチ名**を渡す (コミットのハッシュを渡すと、HTTP 422 `target_commitish is invalid` で失敗する。失敗してもリリースは作られない)
   - **確かめる**: `gh release view` で、タグ・添付の名前と大きさを見る。`git fetch --tags` のあと、タグが指すコミット (`git rev-parse v<版>^{commit}`)が、`origin/master` の先頭と同じであること
   - **報告**: リリースの URL・タグが指すコミット・添付を書く。本文を直すときは、`gh release edit v<版> --notes-file <本文>` (zip とタグは、そのまま)

### 空の DB での確かめ方 (SQL Server)
- 構造を変える操作なので、ユーザーに「作る DB 名・流すスクリプト・消す DB 名」を一覧にして、1 回だけ許可をもらう (共通の決まり)。サーバーは `mimumu202511`、Windows 認証
- テスト用の DB `MmmTool_ddltest` を新しく作り (既存の `MmmTool` は触らない)、次を `sqlcmd -S mimumu202511 -E -C -b -d MmmTool_ddltest -I -f 65001 -i <スクリプト>` で流す。**同じスクリプトを 2 回流して**、2 回目にエラーも追加も出ないこと (何度流しても壊れない)を確かめる: ① MmmTool の `001_CreateTables.sql` ② MmmBatch の `001_CreateTables.sql` (送信の状況の表 `ReminderSendStatus`)
- `SELECT` で、表の一覧と、コードマスタの件数 (作業管理は 18 件)・状態の 4 つを確かめる
- 確かめたら、テスト用の DB を `DROP DATABASE` で消す (`ALTER DATABASE … SET SINGLE_USER WITH ROLLBACK IMMEDIATE` を先に付ける)。残った DB が `MmmTool` だけであることを確かめる
- SQLite は、`py` の `sqlite3` で空のメモリ DB に 2 回流して、表の数・コードマスタの件数・列の並びを確かめる

### つまずきやすい点 (版を上げる作業で起きたこと。2026-10-12)
- **アプリの版と、DB のスクリプトの番号は、別のもの**。スクリプトの番号は DB の版 (SQLite の `PRAGMA user_version`)で、アプリの版 (`Version`)とは関係ない。「初版にまとめる」は、DB のスクリプトを 1 本にする話で、アプリの版を戻す話ではない
- **版の数字は 2 か所だけ** (`Directory.Build.props` と `README.txt` の変更履歴)。ほかの文書に書くと、上げるたびに直し漏れが出る
- **`README.txt` の文字コードは UTF-8 (BOM 付き)・CRLF**。スクリプトで書き換えるときも、そのまま保つ。書き直すと LF・BOM なしになるので、直したあとに必ず確かめる
- **配布物から、GitHub のアドレスを外す**。`README.txt`・`操作説明.html` に、リポジトリを示す案内を書かない。`mimumu` は MIT の著作権表示なので、消さない。同梱ライブラリのライセンス文の URL は、他人の文書なので直さない
- **サーバー保存は、ぼかして書く** (手順・構成・SQL Server の名前は書かない)。外部送信 (ntfy・Discord)は、書かない。ただし、保存先を選ぶ画面は画面に出るので、「通常はローカルを選ぶ」と 1 行だけ書く
- **「修正」に入れるのは、本当におかしな動きをしていたものだけ** (動かない・落ちる・入力できない・違う場所に出る、など)。見た目の統一や、表示・文面のよりよい形への直しは、「変更」に入れる
- **リリース本文の書き方** (2026-10-12。0.2.0 を出したときの、ユーザーの決定)
  - 最初に公開する版は 0.2.0。0.1.0 は「未リリース」と、本文の先頭の補足に書く (「欠番」とは書かない)。zip の `README.txt` の変更履歴にある「0.1.0 初版」の行は、直さずに残す
  - 「必要なもの」と書かない。ランタイム (.NET・Windows App Runtime)は、入っていなければ起動したときの案内からそのまま入れられるので、「事前の準備は要りません」と書く。「必要なもの」の一覧は、読んだ人に「用意するのが面倒」と思わせるため
  - 動作環境は「Windows 11 (x64)」と書く。Windows 10 は、サポートが終わっているため
  - `README.txt` と `README.md` の見出しも「動作環境」にし、同じ書き方に揃えた (ランタイムは、起動時の案内で入れられることを先に書き、リンクは「先に入れる場合」として後ろに置く)
  - 説明書の順番は、最初に「Data フォルダを上書きしないでください」、次に機能一覧。入れ替え方の節は作らない (「配布物には Data が入っていません」の一文で足りる。方法を並べると、利用者の自由を狭める)。当たり前の前置き (「終了してから」など)は書かない
  - 説明書の文章は、配布用の `README.txt` を主にして決める (ユーザーが読んでいるのは `README.txt`。2026-10-12)。文面の相談・報告は `README.txt` で行い、`README.md` とリリース本文は、決まった文に揃えるだけにする (報告では 1 行で触れる)
  - 文章の書き方の決まり (見出し・指示語・「バージョン」・並べすぎない・まちがえる場面から書く・読点など)は、全プロジェクト共通の `~/.claude/CLAUDE.md` の「書き方」に置いた。このプロジェクトでの注意: 利用者が一番まちがえるのは、`Data` が `MmmTool.exe` を起動したフォルダごとに別々にできると知らずに、コピーで `Data` フォルダを上書きして消す場面。注意は、この場面から書く (見出しの例: 「最初に: Data フォルダを上書きしないでください」)
- **変更履歴の文言は、ユーザーが好みを言うたびに増えるので、ここへ書き足す**。今までの好み: 体言止め、括弧を重ねない、「知っておくと便利」のような言い方を避ける (見出しは「操作手順」「補足」)、「〜できるよう修正」のように動詞で終える

## 決定の理由

### フレームワーク依存で配布する
- .NET と Windows App SDK のランタイムを同梱する (自己完結)と、出力のファイル数と容量が大きくなる。**目的は、出力の容量を大きくしないこと**。ランタイムの導入は、配布先でしてもらう。DLL の数そのものは、目的にしない (下の「EXE の横のフォルダと DLL」のとおり、`Lib` に隠すため。2026-10-11)
- かといって、1 つの EXE に全部まとめて巨大にもしたくない (100 MB を超える 1 ファイルにしたくない)ので、単一ファイル化 (`PublishSingleFile`)はしない
- `PublishTrimmed` は、自己完結でないと使えないので使わない。ただし、将来トリミングへ戻しても動くよう、JSON はソース生成のままにする
- ReadyToRun (Release の発行のみ)は使う。DLL の数は変わらず、起動が速くなる (サイズは多少増えるが、目的はランタイムを同梱しないことによる容量の削減で、サイズの切り詰めではない)。「配布物を小さく」は、サイズを最小にする意味ではない。ランタイムを同梱しないことで、全体の容量が自然に小さくなる、という意味で使っている。サイズだけを理由に、起動を速くする設定を外さない
- 見直す条件: ランタイムを入れてもらえない配布先ができたときは、自己完結に切り替える

### EXE の横のフォルダと DLL
EXE の横を、`Assets`(配布物)・`Data`(アプリが書くもの)・`Lib`(パッケージの DLL)の 3 つだけにする。機能・画面・パッケージを足しても、EXE の横が増えない形にするため。ユーザーの方針として、アプリのデータは EXE のフォルダの外 (`%LOCALAPPDATA%` など)には置かない (気づかないところで膨らむため)。
- 以前は `Features`・`Shell`・`MmmSdk.WinUI`・`runtimes`・`MmmTool.exe.WebView2`・`Logs` と、77 個の DLL が EXE の横に並び、どれが必要なものか・自分のアプリのファイルか見分けにくかった
- 画面の XAML (`.xbf`): WinUI のビルドが `.pri` に入れたうえで、ばらのファイルも出力へコピーしている。発行の出力には、もともとばらのファイルが出ない。ビルドの出力からも消して、発行物と同じ形にそろえる (Debug の実行で、配布と同じ読み込み方を確かめられる)。消すのは、「中身が `.xbf`・`.xaml` だけのフォルダ」と EXE の横の `.xbf` に限る (`Assets`・`Data` など、ほかのファイルがあるフォルダには触れない)。フォルダ名を決め打ちにしないので、画面のフォルダが増えても直さなくてよい
- `runtimes`: `RuntimeIdentifier` を `win-x64` にし、ネイティブ DLL (`WebView2Loader.dll` など)を、全 CPU 分の `runtimes\` ではなく、EXE の横に x64 の分だけ置く。出力先のパスには RID を付けない (VS の「発行」が参照先にも RID を渡したときに、参照先の出力先が変わらないようにするため)
- WebView2 のキャッシュは `Data\WebView2`、エラーのログは `Data\Logs`。`Assets` に置かない理由: `Assets` は配布物 (読み取り専用)の置き場で、実行時に書くキャッシュと混ぜると、配布や上書きのときに区別がつかなくなる。`Data` の下にサブフォルダで置く理由: EXE の横のフォルダを増やさない。手で直す JSON (`Data` の直下)とは、フォルダで分かれる。アプリのフォルダごと消せば全部消える
- CLI補助の添付の一時保存 (`%TEMP%\MmmTool\...`)は、そのまま (ユーザーの決定。一時ファイルで、終了時に自分で消すため)
- 使わない部品を入れない: 約 12 個の DLL は、使っていない Windows App SDK の部品 (AI・ML・検索・ウィジェット)のもの。全部入りのパッケージで参照していたため、一緒に入っていた (77 個 → 65 個)
  - 設定ページのカード (`CommunityToolkit.WinUI.Controls.SettingsControls`)も、`Segmented` と同じ版 (8.2.251219)で、同じ依存を持つ。使う csproj (アプリ本体・Reminders・Backlog)に、同じ指定 (全部入りの直接参照と、使わない部品の `ExcludeAssets`)を書く
  - アプリが全部入りのパッケージをやめられない理由: CommunityToolkit (`CommunityToolkit.WinUI.Controls.Segmented`)が古い全部入りのパッケージ (1.6)に依存していて、アプリが参照をやめると 1.6 が入ってきてビルドが通らない。全部入り (2.5.1)を参照して版を決め、使わない部品 (`.AI`・`.ML`・`.Search`・`.Widgets`)と、それらだけが使う依存 (`Microsoft.Windows.AI.MachineLearning`・`System.Numerics.Tensors`)を `ExcludeAssets="all"` で外す
  - SDK は CommunityToolkit の WinUI 部品を使わないので、使う部品のパッケージ (`.WinUI`・`.Foundation`・`.InteractiveExperiences`)だけを参照する。InteractiveExperiences を明示する理由: WinUI 2.3.9 が求める 2.1.8 は公開されておらず、NuGet が警告 (NU1603)を出して 2.1.9 を選ぶ。全部入り 2.5.1 と同じ 2.1.9 を明示する
- `Lib` への移動: パッケージのファイルと、自分のプロジェクト・パッケージ以外の参照 (`MmmTool.<機能>`・`MmmTool.<機能>.Core`・`MmmSdk.*` とその `.pri`・`.xml`、`Microsoft.Web.WebView2.Core.Projection`)と、Windows SDK・WinRT (ランタイムパックの分。発行では別の経路 (`RuntimePackAsset`)でコピーされる)を、それぞれ csproj のターゲットでコピー先を変える
  - EXE の横に残る `MmmTool.exe`・`MmmTool.dll`・`MmmTool.deps.json`・`MmmTool.runtimeconfig.json`・`MmmTool.pri` は、.NET・WinUI が EXE の横から読む。`Microsoft.Web.WebView2.Core.dll` は deps.json に載らないネイティブの部品で、WinUI の WebView2 が EXE のフォルダから読むので、残す。`MmmTool.pri` を EXE の横に残すのは、WinUI が EXE の横から画面とリソースを読むため (ユーザーの決定)
  - 起動時に .NET が `Lib` の DLL を見つけられるよう、`deps.json` の各ファイルに `localPath`(ファイルの実際の置き場所。.NET のホスト (hostpolicy)が読む項目)を書き足す。.NET SDK には、これを書く設定が無い (コピー先を変えても、deps.json には書かれない)ので、ビルドの手順の中で書き足す。小さなコンソールアプリで、`localPath` があれば `Lib` から読まれ、無ければ起動時に見つからないことを確かめた。Release を起動し、WinRT・WinUI・Windows SDK・Windows App Runtime の起動用の DLL が `Lib` から読まれることも確かめた
  - 書き足しは、JSON を読み書きせず文字列で行う (VS の MSBuild (.NET Framework)でも動くインラインタスクにするため)。deps.json は .NET SDK が決まった形で書くので、ファイル名をキーにして足せる。すでに足してあれば何もしない (ビルドを繰り返しても二重にならない)
  - 衛星アセンブリ (`MoveSatellitesToLib`)も同じ仕組みで、deps.json の `resources` の項目に `localPath`(`Lib/ja/…`)を書き足す。SQL Server のドライバー (`Microsoft.Data.SqlClient`)を参照したとき、`ja/` が EXE の横にできたため足した。小さなコンソールアプリで、`Lib/ja/` から読まれ、メッセージが日本語になることと、ネイティブの `Microsoft.Data.SqlClient.SNI.dll` が `Lib` から読まれ、DB に接続できることを確かめた。日本語のままにするのは、ドライバーが出す接続の失敗の文を、そのまま画面に出す場合があるため
  - 公式の設定ではないので、.NET SDK の更新で deps.json の形が変わると、効かなくなるおそれがある。そのときは起動時に DLL が見つからずに落ちるので、気づける。`MovePackageFilesToLib`・`MoveRuntimePackFilesToLib`・`MoveReferencesToLib`・`MoveSatellitesToLib`・`AddLocalPathToBuildDepsFile`・`AddLocalPathToPublishDepsFile` を消せば、元の形 (EXE の横に並ぶ)に戻る
- `.xml`(XML ドキュメントコメント)は、実行に使わないので、ビルドの出力にコピーしない (検査のために obj には作る)。Release では `.pdb` を出力に入れない (ユーザーの決定。Debug では入れる)
- **DLL の数は、パッケージを足す・足さないの理由にしない** (ユーザーの決定。2026-10-11)。パッケージの DLL (マネージド・ネイティブとも)は `Lib` に置くので、足しても EXE の横は増えず、見分けにくくならない。ネイティブ DLL も `Lib` から読まれることは、SQL Server のドライバーの `Microsoft.Data.SqlClient.SNI.dll` で確かめてある。新しいパッケージのネイティブ DLL は、足したあとの発行で、`Lib` から読まれることを確かめる
- 最終形にどう近づくか: EXE の横は、`MmmTool.*` の 5 ファイルと `Microsoft.Web.WebView2.Core.dll`・`Assets`・`Data`・`Lib` だけになり、パッケージを足しても EXE の横は増えない。使わない Windows App SDK の部品は入らない

### 更新と発行のバッチをリポジトリに入れる
- `scripts/update_and_publish.bat` は、リポジトリに入れる (クローンの外の親フォルダには置かない)。別の PC にクローンしたときも、そのまま取得から発行までを実行できるようにするため (ユーザーの要望)。最終形では開発用の補助スクリプトが複数になるので、直下ではなく `scripts/` に置く。アプリが使う `src/Plugins/MmmTool.CliAssist/Tools/` の `.ps1` とは別
- バッチは、最初に自分を `%TEMP%` へコピーし、コピーから続きを実行する。cmd は実行中のバッチを位置で読み直すので、`git pull` でバッチ自身が書き換わると、行の途中から読んで誤動作するため
- `.gitattributes` で `*.bat` を CRLF にそろえる (LF だと cmd が行を読み違えることがある)。バッチの表示文は ASCII のみにする (コードページの違いで文字化けしないため)
