# リマインダー

指定した日時（または曜日・時刻）に通知し、今日の分を一覧して対応状態（未・スヌーズ・完了）を管理する機能。データと監視、入力画面、一覧画面、メイン画面、設定（スヌーズ間隔）からなる。`Features/Reminders/`（Core 側は `MmmTool.Core/Reminders/`）。

## データ
- `Data/Reminders.json` / `Data/ReminderStates.json`：どちらも `{ "items": [...] }`（ラッパー型は `JsonReminderRepository.cs` 内の internal）
- `Reminder`（record。複製しやすいため）
  - `Seq`（ID の連番）・`No`（参照番号）・`IsDeleted`・`Date`（yyyyMMdd。曜日指定は `NoDate` = 99999999）・`Time`（HHmm）・`Weekdays`・`Title`・`Note`・`Link`
- `ReminderState`：`Seq`・`BaseNo`・`Date`・`Status`
- `Weekdays` は `[Flags]`（月 = 1 … 日 = 64）、`ReminderStatus` は None = 0 / Done = 1 / Snooze = 2。JSON には数値で入る
- 変換は `ReminderDates`（`DateOnly` / `TimeOnly` / `DayOfWeek` との相互変換、`ToJapanese` で「月火水」、`DescribeWeekdays` で画面に出す曜日の文字列＝曜日が 1 つも無ければ「毎日」）
- 発動の判定は `ReminderDates.OccursOn(Reminder, DateOnly)`：発動日が今日、または曜日指定で今日の曜日を含む。曜日が 1 つも無い曜日指定は毎日。監視とメイン画面で共通に使う

## サービス（`ReminderService`、Singleton）
- 最初のアクセスで両ファイルを読んでキャッシュし、`SemaphoreSlim` で順番に読み書きする。取得はキャッシュの複製を返す
- 保存：`Seq` が 0 なら最大 + 1 で採番して `No` も同じ値にする。既存は `No` を保ったまま更新し、削除フラグを解除する（無い `Seq` は `ArgumentException`）
- 論理削除 `DeleteAsync`、物理削除 `PurgeAsync`（状態は残る）
- 状態は `BaseNo` ごとに 1 件で上書きする（`SetStateAsync`）
- 変更後に `Changed` を発火する（任意のスレッドから）
- IO エラーで読めなかったときは、空として扱い `LoadError` に残す。保存は `DataFileException`（元データを消さないため）

## 監視（`ReminderMonitor`、Singleton）
- 起動時の準備 `ReminderStartup`（`IStartupTask`）で `Start(コールバック)`。コールバックは `DispatcherQueue` で UI スレッドへ移し、`INotificationDialogService.Show(タイトル, 項目)` を呼ぶ。Host の破棄で停止する
- `TimeProvider` の単発タイマー：開始直後に 1 回、以後は毎分 00 秒に判定する。次の 00 秒までの時間をその都度計算して掛け直す（早く来て同じ分になったら、判定せず掛け直すだけ）
- 対象：発動する日で、時刻が来たもの。並びは時刻 → `No`
- 今日の日付の状態だけを見る（別の日の状態は None 扱い）
  - None：毎分通知する
  - Done：通知しない
  - Snooze：「モニター全体で共有する前回のスヌーズ分の通知」から間隔以上たったら通知する（メモリ上だけ。起動直後の 1 回目は前回が無いので通知する）
- スヌーズ間隔は設定ストアの `Reminder.SnoozeIntervalMinutes` を毎回読む（既定 15、範囲は 5〜999。キー・定数・補正は `ReminderSettingsService` に集約している）
- 1 回の判定の対象は 1 つの通知にまとめる。タイトルは「リマインダー」、本文は件名（リンクがあればリンク先つき）
- 判定中の想定外の例外は握りつぶさない（`async void` なので未処理例外になる）
- `SnoozeTriggeredAsync`：発動済みで今日の状態が None のものを Snooze にし、モニターのスヌーズ間隔をその分から数え直す（そうしないと次の分にすぐ再通知される）。通知をクリックしてメイン画面を開いたときに呼ぶ。行で手動でスヌーズにしたときは数え直さない

## 擬似モーダル
一覧・入力画面・確認ダイアログは、開いている間、親を操作できない擬似モーダルにする。決めた理由は [../decisions/0005-pseudo-modal.md](../decisions/0005-pseudo-modal.md)。

- `Services/PseudoModal`（ウィンドウに付ける部品）。`SetOwner`（`GWLP_HWNDPARENT`）で親を設定し、表示したら `EnableWindow(親, false)` で親を操作不可にし、閉じる前に戻して親を前面に出す
  - コードから閉じるときは `PseudoModal.Close()` を `Close()` の前に呼ぶ。× / Alt+F4 は `AppWindow.Closing`、念のため `Closed` でも戻す。自分が消える前に戻さないと、別のアプリが前面に来る
- 親の中央に出し、作業領域からはみ出す分は内側へ寄せる（`CenterOnOwner`）。重ねてよい（メイン → 一覧 → 入力画面 / 確認）
- 親は `DialogService` が決める：開いているモーダルウィンドウを開いた順に覚えておき、いちばん手前を親にする。無ければ、最後にアクティブになった普通のウィンドウ（`TrackWindow` で覚えたメインウィンドウかリマインダーのメイン画面）。確認ダイアログ（`ContentDialog`）・作業ディレクトリ変更ダイアログも同じ親の `XamlRoot` に出す

## 入力画面（`ReminderInputWindow` ＋ `ReminderInputViewModel`）
- 開くのは `IReminderDialogService.ShowInputAsync(対象 or null)`。保存した内容 or null（キャンセル）を返す。`Seq` が 0 の内容を渡すと、それを初期値にした新規（コピーして新規追加）になる
- 独立したウィンドウ（Mica・開くたびに DI から作る Transient）。擬似モーダル・`OverlappedPresenter.CreateForDialog()`
- 幅 480 固定。高さは読み込み時に中身を測って固定する（日付指定・曜日指定の欄は高いほうの高さを確保、件名のエラー行も場所を空けておく。切り替えやエラーで下の欄・ボタンが動かないように）
- タイトル帯（`SetTitleBar`）をドラッグして移動できる。× はキャンセルと同じで、確認なし。Esc / Enter のショートカットは付けない
- タイトル：新規は「リマインダー入力」、採番済みは「リマインダー編集」
- 初期値：新規は曜日指定（「日付を指定する」はオフ）・曜日は未選択・日付欄は今日・時刻は現在時刻・ほかは空。編集は対象の値（曜日指定なら日付欄は今日）
- 曜日を 1 つも選ばない曜日指定は、毎日通知する
- 時刻は自作の `Controls/TimeInputBox`。決めた理由は [../decisions/0006-time-input-box.md](../decisions/0006-time-input-box.md)
  - 1 つの枠に「時 : 分」の 2 区画。ボタンなし。数字だけ・2 桁打つと分へ・←→ で区画移動・↑↓ / ホイールで ±1（端で回る）・フォーカスで全選択と IME オフ・離れたときに確定（空なら元の値・範囲外は最大値）・常に 2 桁表示
  - 区画は枠・消去ボタンを持たない最小テンプレートの `TextBox`。外側の枠が、標準の入力欄の見た目（ポインタ上・フォーカス中の背景とアクセントの下線）を受け持つ
- 日付は `CalendarDatePicker`（選択中の日付を押して空になったら元に戻す）
- 件名は前後の空白を除いて、空ならエラー（件名欄の下に赤字。件名を変えたら消す）
- 保存の失敗（読み込み失敗中・IO エラー・編集中に対象が完全削除）は、上部の InfoBar で知らせて閉じない
- IME：件名・備考はフォーカスでオン、リンクはオフ（`NativeMethods.TurnOn/TurnOffImeForFocusedWindow`）
- 保存しても、今日の対応状態はそのまま

## 一覧画面（`ReminderListWindow` ＋ `ReminderListViewModel`）
- 開くのは `IReminderDialogService.ShowListAsync()`。常にモーダル（2 枚目は開けない）。Transient で、閉じたら `Dispose` で `ReminderService.Changed` の購読をやめる
- Mica・タイトル帯でドラッグ。大きさは変えられる（最初 760×560・最小 560×360 DIP、最大化・最小化なし）
- 上に「新規追加」（Accent）、右に「過去の予定を表示」「削除済みを表示」の `ToggleSwitch`（どちらも既定オフ。状態は保存しない）
  - 「過去の予定」をオフにすると、日付が昨日以前の日付指定を隠す。今日の分は時刻が過ぎていても出す。過去日が日付順で一番上に並んで邪魔になるため（並び替えで下へ回す案より、隠す案にした）
- 下にカード状の一覧。見出しは固定で、行だけスクロールする。列は 日付 120 / 曜日 120 / 時刻 80 / 件名（残り・省略記号）
  - 日付は `yyyy/MM/dd` か「－」。曜日は「月火水」。曜日を選んでいない曜日指定は「毎日」（日付・曜日とも「－」だと区別できないため）。日付指定の曜日は「－」
  - 並びは 日付 → 時刻 → 連番（曜日指定は `NoDate` なので後ろ）
  - 削除済みは不透明度 0.5 + 取り消し線
- 行のダブルタップで編集。右クリック（メニューキー）は、その行を選択してからメニューを出す（リンクのツリーと同じく、`AddHandler(..., handledEventsToo: true)` で `RightTapped` と `ContextRequested` を受ける）
- 右クリックのメニュー：削除済みでない行は「編集 / コピーして新規追加 / 削除」、削除済みの行は「編集 / コピーして新規追加 / 完全削除」
  - 削除済みの行の編集は、「保存すると削除が取り消される」確認（キャンセルが既定）をしてから開く
  - 論理削除は確認なし。完全削除は確認あり
  - コピーして新規追加は、日付・時刻・曜日・件名・備考・リンクだけを引き継ぐ（連番・削除フラグは引き継がず、元は変えない）
- メニュー項目は画面の外に出て DataContext が来ないので、行を `Tag="{x:Bind}"` で渡す
- 変更（`Changed`）は、ViewModel を作ったスレッドの `SynchronizationContext` に戻して読み直し、変わった行だけ差し替える（全部作り直すとスクロールが先頭へ戻るため）
- エラー（読み込み失敗・壊れたファイルの退避・保存の失敗）は、一覧の上に重ねる InfoBar で知らせる（閉じたら消える）
- 空の一覧が一瞬見えないよう、読み込んでから表示する

## メイン画面（`ReminderMainWindow` ＋ `ReminderMainViewModel`）
- 開くのは `ReminderWindowService`（Singleton）。1 枚だけ持ち、開いていれば前面に出す。読み込み中の 2 回目の呼び出しは、表示し終わるのを待つ
- トレイメニューの「リマインダー」（`ReminderTrayMenuSource`。リンクより上）と、通知の本文クリックから開く
- モーダルではない普通のウィンドウ（× は普通に閉じる。トレイへの退避ではない）。Mica・タイトル帯でドラッグ
- 大きさは変えられるが保存しない（開くたびに最初の大きさ。最初 360×440・最小 360×320 DIP、最大化・最小化なし、主モニターの作業領域の中央。表示前に倍率を知るため `GetDpiForWindow` を使う）
- 今日の対象を、時刻 → `No` の順に表示する。未対応（未・スヌーズ）を上、完了を下の `Expander`（既定は閉じる・完了が 1 件以上のときだけ「完了 (件数)」）。0 件のときは「今日のリマインダーはありません。」
- レイアウトの寸法：タイトル帯の Padding 14,10,_,2・操作行の Margin 14,6,14,8（ボタンの Padding 10,4・アイコン 13 + 文字・間 6）・区切り線（`DividerStrokeColorDefaultBrush`）・本体の Margin 10,0,10,10・行は高さ 48・角丸 8・行間 6
- 状態の切り替えの各ボタンは Padding 10,4・文字 12・MinWidth 0（暗黙スタイル。`BasedOn` はライブラリのキーを参照できない恐れがあるので付けない）
- 行はカード
  - 状態の縦線（幅 6・角丸 3）。色は固定で、未 `#9E9E9E` / スヌーズ `#F59E0B` / 完了 `#16A34A`。完了の緑は、濃い緑（`#2E7D32` など）だとダークテーマで沈むため、明るいエメラルド系にしている
  - 時刻（15 SemiBold）
  - 件名（13 SemiBold）：`Controls/LinkArea`（Grid 派生）。リンクがあると手のカーソル + ホバー背景で、押すと開く。完了は取り消し線 + 不透明度 0.5
  - 状態の切り替え：CommunityToolkit の `Segmented`（NuGet `CommunityToolkit.WinUI.Controls.Segmented`）を `StatusIndex` と双方向でつなぐ。ユーザーが変えたときだけ `SetStateAsync` を呼び、読み直しの反映では保存しない
- 右クリック：リンクを開く（リンクのある行だけ有効）/ 編集 / 削除（論理削除・確認なし。一覧と同じ）
- 変更は差分更新する（`No` で同定・不足は挿入・余剰は削除・順番は `Move`。完了の欄の開閉を保つため）。行は `ReminderTodayItem`（読み直しでは `Apply` で中身だけを差し替える）
- 日付が変わったら新しい日に切り替える（読み直すたびに、次の 0 時 + 1 秒のタイマーを掛け直す）
- 通知との連携：通知の本文クリックの `onClicked` から、`DispatcherQueue`（Low）でいったん後回しにして `ShowFromNotificationAsync` を呼ぶ（閉じかけの通知ウィンドウと、別のウィンドウの操作が重ならないように）。開いてから `ReminderMonitor.SnoozeTriggeredAsync` を呼ぶ
- ダイアログの親：`DialogService.TrackWindow` で普通のウィンドウ（メインウィンドウ・この画面）を覚え、モーダルが無いときは最後にアクティブになったほうを親にする（この画面から開いた一覧・入力・確認が、この画面の上に出るように）

## 設定
スヌーズ間隔は設定ページ（[settings.md](settings.md)）で変える。
