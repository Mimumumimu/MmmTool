# 未実装・残りの作業

これからやることだけを書く。実装したら消し、説明は該当の `docs/` に移す。実装済みの機能は `docs/specs/` を見る。

## 不具合の調査

- 通知をクリックしても、開いたままのリマインダーの画面が、他のウィンドウの後ろに残ることがある
  - 症状の出る端末と出ない端末がある。同じ端末でも、前面になるときと、ならないときがある。DB モード。
  - 原因の見立て: `BringToFront` の `SetForegroundWindow` が Windows に断られている。通知ウィンドウはフォーカスを奪わない作りで、クリックから前面化までに、Low 優先度の待ちが 2 回入る。
  - 調べるために、前面化の結果を `Data/Logs/foreground-yyyy-MM-dd.log` に書く計測を入れてある。
    - 計測のコード: `WindowExtensions.BringToFrontTraced`・`ReminderWindowService.WriteTrace`・`ReminderWindowService.MarkClicked`
    - コメントに `TEMP(計測)` と書いてある。
  - 症状の出る端末で、リマインダーの画面を開いたまま通知をクリックして、ログを見る。
    - `SetForegroundWindow=False`: 前面化の権利が無い。待ちを挟む前の、クリックの処理の中で前面化する作りにする。保険として、Windows App SDK 2.1.9 にある `AppWindow.MoveInZOrderAtTop` を使う。
    - `SetForegroundWindow=True` なのに後ろに残る: 前面化のあとに、通知を閉じる `CloseAfterClick` か作り直しの `Present` が、前面を奪っている。順序を見直す。
  - 直したら、計測のコードとログを消し、`BringToFront` に戻す。
  - 閉じている画面を新しく開く経路は `Activate()` だけで `BringToFront` を通らない。docs/specs/reminders.md の「前面に出すのは SDK の `BringToFront`」と食い違うので、直すときにそろえる。

## そのうち (方向だけ)

- Web 版の画面を、スマホ向けのサイトとして、あらためて見直す
  - 方向: 使いやすさを第一に、触って分かる操作と、今のスマホアプリの標準に合わせる
  - 見直しの基準は、`~/.claude/skills/mobile-ui/` のスキル `mobile-ui`。画面ごとではなく、全体として見直す。
  - メッセージの Web 画面を、同じ基準で見直す。
- サーバー側のビルド (GitHub Actions)で、タグを付けたら発行からリリースまで自動で行う。ユーザーが試したがっている。リポジトリは公開で、Actions は無料。
  - 調べること: 手元と同じ配布物になるか、`dotnet publish` の時間、SDK の取得
  - 最初は、zip を実行結果の添付に置くだけの手動実行で試す
- MmmBatch を MmmToolBatch に改名する
  - アプリの名前を MmmTool・MmmToolWeb とそろえるため
  - 直す範囲は、フォルダ・プロジェクト・名前空間・文書・DB のログイン名
- メッセージの会話の切り替えで入れた「重ねたフェード」を、アプリ全体の切り替えに広げる。共通の処理にできるかを検討する
  - 方向: 画面の一部の中身を入れ替えるとき、前の見た目を画像に撮って重ね、下で差し替えてから、薄くしながら新しい中身を濃くする。空の瞬間を見せない。
  - 仕組みは、メッセージのページ (`MessagesPage.xaml.cs` の `PrepareSwitchAsync`・`CrossFadeAsync`・`FadeAsync`)と、ViewModel の `PrepareSwitchAsync`・`MessagesReplaced` にある。決めたことは [specs/messages.md](specs/messages.md) の「人の一覧」にある。
  - 検討すること
    - SDK の `MmmSdk.WinUI` に、部品 (切り替え用のコンテナ、または添付の仕組み)として置けるか
    - 切り替えが要る画面の候補を洗い出す (別の中身に入れ替わる一覧・詳細・ページ)
    - 長さと動きの形を共通の値にそろえる。メッセージのページでは、退場 260・入場の遅れ 60・入場 360 ミリ秒
    - 「視覚効果を減らす」の設定のとき、動きを止めるか
    - 画像を撮る方式の限界 (撮れない部品・大きな画面のメモリ)

## ユーザーが用意するもの待ち

- 管理ツール (MmmToolAdmin)のアイコン: いまは MmmBatch のアイコンを仮に使っている (`src/App/MmmToolAdmin/Assets/app.ico`)
