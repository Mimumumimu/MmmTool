# リマインダー

指定した日時（または曜日・時刻）に通知し、今日の分を一覧して対応状態（未・スヌーズ・完了）を管理する機能。データと監視、入力画面、一覧画面、メイン画面、設定（スヌーズ間隔）からなる。`Features/Reminders/`（中は `Main/`・`Input/`・`List/` に分けている。Core 側は `MmmTool.Core/Reminders/`）。

## データ
- `Data/Reminders.json` / `Data/ReminderStates.json`：どちらも `{ "items": [...] }`（ラッパー型は `Json/ReminderFile.cs`・`Json/ReminderStateFile.cs` の internal）
- `Reminder`（record。複製しやすいため）
  - `No`（番号。登録順の一意の連番で、状態の `BaseNo` が指すキー。0 は未採番）・`IsDeleted`・`Date`（yyyyMMdd。曜日指定は `NoDate` = 99999999）・`Time`（HHmm）・`Weekdays`・`Title`・`Note`・`Link`
- 値は作ったあとに書き換えず、`with` で新しく作る（`init`）。`ReminderState`：`Seq`・`BaseNo`・`Date`・`Status`
- `Weekdays` は `[Flags]`（月 = 1 … 日 = 64）、`ReminderStatus` は None = 0 / Done = 1 / Snooze = 2。JSON には数値で入る
- 変換は `ReminderDates`（`DateOnly` / `TimeOnly` / `DayOfWeek` との相互変換、`ToJapanese` で「月火水」、`DescribeWeekdays` で画面に出す曜日の文字列＝曜日が 1 つも無ければ「毎日」）
- 発動の判定は `ReminderDates.OccursOn(Reminder, DateOnly)`：発動日が今日、または曜日指定で今日の曜日を含む。曜日が 1 つも無い曜日指定は毎日。監視とメイン画面で共通に使う

## 保存先（`IReminderRepository`）
- 1 件単位の操作で書く（一覧を丸ごと置き換えない）：`AddAsync`・`UpdateAsync`・`SetDeletedAsync`（論理削除の切り替え）・`PurgeAsync`（物理削除）・`SetStatesAsync`（対応状態のまとめ書き）。読みは `GetRemindersAsync(includeDeleted)`・`GetStatesAsync`
- 番号（`Reminder.No`）と対応状態の連番（`ReminderState.Seq`）は、保存先が決める（`AddAsync` は `No` を無視し、番号つきの内容を返す）。JSON は、ロックの中で「最大 + 1」を計算する。SQL Server は IDENTITY かトランザクション内の最大 + 1（一意制約と再試行）、DynamoDB はカウンター項目の原子的な加算。型は `int` のまま（人が登録する件数で、`int` の範囲を超えないため。DB 側も `int` の IDENTITY で足りる）
- `PurgeAsync` は対応状態も一緒に消す。残すと、最大の番号を消したあとの新規作成で同じ番号が採番されたとき、前の状態を引き継ぐため。JSON は状態のファイルを先に書く（途中で失敗しても、状態が残るだけの側に倒す）
- 読み書きはスレッドセーフ。返す値は `init` の record なので、複製しない
- JSON 実装（`JsonReminderRepository`）: 最初のアクセスで両ファイルを読んでメモリに持ち、`SemaphoreSlim` で順番に読み書きする。書き込みは、変更後のコピーをファイルに書き、成功してからメモリを差し替える（保存に失敗しても、メモリだけが新しい状態にならない）
- IO エラーで読めなかったときは、空として扱い `LoadError` に残す。書き込みは `DataFileException`（元データを消さないため）。読み込み結果は SDK の `LoadStatus` で持つ（ファイル固有の仕組みなので、保存先の中に閉じる。ファイルを使わない保存先は、`LoadError` / `RecoveryMessage` を常に null にする）

## サービス（`ReminderService`、Singleton）
- 保存先の 1 件単位の操作に、業務の決まりを載せる。一覧は持たず、そのつど保存先から読む（排他・採番・読み込みの失敗の扱いは保存先の仕事）
- 保存：`No` が 0 なら追加（`AddAsync`）。既存は同じ `No` を更新し、削除フラグを解除する（無い `No` は `ArgumentException`）
- 論理削除 `DeleteAsync`、物理削除 `PurgeAsync`
- 状態は `BaseNo` ごとに 1 件で上書きする（`SetStateAsync`。複数件は `SetStatesAsync` で 1 回の書き込み）
- 変更後に `Changed` を発火する（任意のスレッドから）
- `LoadError` / `RecoveryMessage` は保存先のものを返す（最初に読んだあとに分かる）
- 時刻（`HHmm` の整数）が正しくない（手で編集した JSON で負の値・25 時など）リマインダーは、「常に発動済み」のように誤って動かないよう、`GetTargetsAsync`（今日の対象・通知）から外す。本体は消さない（一覧には出る。編集して直せる）。読むたびに調べ、あれば番号を挙げた警告を `RecoveryMessage` に足して、InfoBar で知らせる
- `GetTargetsAsync(now)`：ある日の対象（その日に発生するリマインダー）と、その日の対応状態（`ReminderTarget`）を、時刻 → `No` の順で返す。メイン画面と通知（`ReminderMonitor`）の両方がこれを使うので、「今日の状態」の組み立てはここだけ

## 監視（`ReminderMonitor`、Singleton）
- 役割分担：タイマーの管理は SDK の `MinuteScheduler`（毎分 00 秒の単発タイマーの掛け直し・同じ分に 2 回呼ばない）、通知する項目の判定は `ReminderEvaluator`（時刻・タイマー・設定に依存しない純粋な判定）、`ReminderMonitor` はそれらをつなぎ、スヌーズの通知時刻を覚える。スヌーズ間隔は `ReminderSettingsService` から読む（設定ストアに直接触れない）
- 起動時の準備 `ReminderStartup`（`IStartupTask`）で `Start(コールバック)`。コールバックは `DispatcherQueue` で UI スレッドへ移し、`INotificationDialogService.Show(タイトル, 項目)` を呼ぶ。Host の破棄で停止する
- `TimeProvider` の単発タイマー：開始直後に 1 回、以後は毎分 00 秒に判定する。次の 00 秒までの時間をその都度計算して掛け直す（早く来て同じ分になったら、判定せず掛け直すだけ）
- 対象：発動する日で、時刻が来たもの（`GetTargetsAsync` の結果のうち、時刻が過ぎたもの）。並びは時刻 → `No`
- 今日の日付の状態だけを見る（別の日の状態は None 扱い）
  - None：毎分通知する
  - Done：通知しない
  - Snooze：「モニター全体で共有する前回のスヌーズ分の通知」から間隔以上たったら通知する（メモリ上だけ。起動直後の 1 回目は前回が無いので通知する）
- スヌーズ間隔は設定ストアの `Reminder.SnoozeIntervalMinutes` を毎回読む（既定 15、範囲は 5〜999。キー・定数・補正は `ReminderSettingsService` に集約している）
- 1 回の判定の対象は 1 つの通知にまとめる。タイトルは「リマインダー」、本文は件名（リンクがあればリンク先つき）
- 判定中の想定外の例外は握りつぶさない（`async void` なので未処理例外になる）
- `SnoozeTriggeredAsync`：発動済みで今日の状態が None のものを Snooze にし、モニターのスヌーズ間隔をその分から数え直す（対象の状態は、`SetStatesAsync` で 1 回の書き込みにまとめる）（そうしないと次の分にすぐ再通知される）。通知をクリックしてメイン画面を開いたときに呼ぶ。行で手動でスヌーズにしたときは数え直さない

## 擬似モーダル
一覧・入力画面・確認ダイアログは、開いている間、親を操作できない擬似モーダルにする。決めた理由は [../decisions/0005-pseudo-modal.md](../decisions/0005-pseudo-modal.md)。

- SDK の `PseudoModal`（`MmmSdk.WinUI.Dialogs`。ウィンドウに付ける部品。詳細は SDK の `docs/dialogs.md`）。`SetOwner`（`GWLP_HWNDPARENT`）で親を設定し、表示したら `EnableWindow(親, false)` で親を操作不可にし、閉じる前に戻して親を前面に出す
  - コードから閉じるときは `PseudoModal.Close()` を `Close()` の前に呼ぶ。× / Alt+F4 は `AppWindow.Closing`、念のため `Closed` でも戻す。自分が消える前に戻さないと、別のアプリが前面に来る
- 親の中央に出し、作業領域からはみ出す分は内側へ寄せる（`CenterOnOwner`）。重ねてよい（メイン → 一覧 → 入力画面 / 確認）
- 親は SDK の `IDialogHost`（実装は `DialogService`）が決める：開いているモーダルウィンドウを開いた順に覚えておき、いちばん手前を親にする。無ければ、最後にアクティブになった普通のウィンドウ（`TrackWindow` で覚えたメインウィンドウかリマインダーのメイン画面）。確認ダイアログ（`ContentDialog`）・作業ディレクトリ変更ダイアログも同じ親の `XamlRoot` に出す

## 入力画面（`ReminderInputWindow` ＋ `ReminderInputViewModel`）
- 開くのは `IReminderDialogService.ShowInputAsync(対象 or null)`。保存した内容 or null（キャンセル）を返す。`No` が 0 の内容を渡すと、それを初期値にした新規（コピーして新規追加）になる
- 独立したウィンドウ（Mica・開くたびに DI から作る Transient）。擬似モーダル・`OverlappedPresenter.CreateForDialog()`
- 幅 480 固定。高さは読み込み時に中身を測って固定する（日付指定・曜日指定の欄は高いほうの高さを確保、件名のエラー行も場所を空けておく。切り替えやエラーで下の欄・ボタンが動かないように）
- タイトル帯（`SetTitleBar`）をドラッグして移動できる。× はキャンセルと同じで、確認なし。Esc / Enter のショートカットは付けない
- タイトル：新規は「リマインダー入力」、採番済みは「リマインダー編集」
- 初期値：新規は曜日指定（「日付を指定する」はオフ）・曜日は未選択・日付欄は今日・時刻は現在時刻・ほかは空。編集は対象の値（曜日指定なら日付欄は今日）
- 曜日を 1 つも選ばない曜日指定は、毎日通知する
- 時刻は SDK の `MmmSdk.WinUI.Controls.TimeInputBox`。決めた理由は [../decisions/0006-time-input-box.md](../decisions/0006-time-input-box.md)
  - 1 つの枠に「時 : 分」の 2 区画。ボタンなし。数字だけ・2 桁打つと分へ・←→ で区画移動・↑↓ / ホイールで ±1（端で回る）・フォーカスで全選択と IME オフ・離れたときに確定（空なら元の値・範囲外は最大値）・常に 2 桁表示
  - 区画は枠・消去ボタンを持たない最小テンプレートの `TextBox`。外側の枠が、標準の入力欄の見た目（ポインタ上・フォーカス中の背景とアクセントの下線）を受け持つ
- 日付は `CalendarDatePicker`（選択中の日付を押して空になったら元に戻す）
- 件名は前後の空白を除いて、空ならエラー（件名欄の下に赤字。件名を変えたら消す）
- 保存の失敗（読み込み失敗中・IO エラー・編集中に対象が完全削除）は、上部の InfoBar で知らせて閉じない
- IME：件名・備考はフォーカスでオン、リンクはオフ（SDK の `ImeControl.TurnOn/TurnOff`）
- 保存しても、今日の対応状態はそのまま

## 一覧画面（`ReminderListWindow` ＋ `ReminderListViewModel`）
- 開くのは `IReminderDialogService.ShowListAsync()`。常にモーダル（2 枚目は開けない）。Transient で、閉じたら `Dispose` で `ReminderService.Changed` の購読をやめる
- Mica・タイトル帯でドラッグ。大きさは変えられる（最初 760×560・最小 560×360 DIP、最大化・最小化なし）
- 上に「新規追加」（Accent）、右に「過去の予定を表示」「削除済みを表示」の `ToggleSwitch`（どちらも既定オフ。状態は保存しない）
  - 「過去の予定」をオフにすると、日付が昨日以前の日付指定を隠す。今日の分は時刻が過ぎていても出す。過去日が日付順で一番上に並んで邪魔になるため（並び替えで下へ回す案より、隠す案にした）
- 下にカード状の一覧。見出しは固定で、行だけスクロールする。列は 日付 120 / 曜日 120 / 時刻 80 / 件名（残り・省略記号）
  - 日付は `yyyy/MM/dd` か「－」。曜日は「月火水」。曜日を選んでいない曜日指定は「毎日」（日付・曜日とも「－」だと区別できないため）。日付指定の曜日は「－」
  - 並びは 日付 → 時刻 → 番号（曜日指定は `NoDate` なので後ろ）
  - 削除済みは不透明度 0.5 + 取り消し線
- 行のダブルタップで編集。右クリック（メニューキー）は、その行を選択してからメニューを出す（リンクのツリーと同じく、`AddHandler(..., handledEventsToo: true)` で `RightTapped` と `ContextRequested` を受ける）
- 右クリックのメニュー：削除済みでない行は「編集 / コピーして新規追加 / 削除」、削除済みの行は「編集 / コピーして新規追加 / 完全削除」
  - 削除済みの行の編集は、「保存すると削除が取り消される」確認（キャンセルが既定）をしてから開く
  - 論理削除は確認なし。完全削除は確認あり
  - コピーして新規追加は、日付・時刻・曜日・件名・備考・リンクだけを引き継ぐ（番号・削除フラグは引き継がず、元は変えない）
- メニュー項目は画面の外に出て DataContext が来ないので、行を `Tag="{x:Bind}"` で渡す
- 変更（`Changed`）は、ViewModel を作ったスレッドの `SynchronizationContext` に戻して読み直し、変わった行だけ差し替える（全部作り直すとスクロールが先頭へ戻るため）
- エラー（読み込み失敗・壊れたファイルの退避・保存の失敗）は、一覧の上に重ねる InfoBar で知らせる（閉じたら消える）
- 空の一覧が一瞬見えないよう、読み込んでから表示する

## メイン画面（`ReminderMainWindow` ＋ `ReminderMainViewModel`）
- 開くのは `ReminderWindowService`（Singleton）。1 枚だけ持ち、開いていれば前面に出す。読み込み中の 2 回目の呼び出しは、表示し終わるのを待つ
  - 読み込みの失敗（ファイルを読めない等）は保存先が受けて `LoadError` に残す（空として続け、画面の InfoBar に出す）ので、ここまでは例外にならない。画面を作る・表示するのが失敗したとき（バグ）は、作りかけの画面を閉じ、失敗した結果を残さずに（次の呼び出しで作り直せるように）、例外はそのまま呼び出し元へ渡す
- トレイメニューの「リマインダー」（`ReminderTrayMenuSource`。リンクより上）と、通知の本文クリックから開く
- モーダルではない普通のウィンドウ（× は普通に閉じる。トレイへの退避ではない）。Mica・タイトル帯でドラッグ
- 大きさは変えられるが保存しない（開くたびに最初の大きさ。最初 360×440・最小 360×320 DIP、最大化・最小化なし、主モニターの作業領域の中央。表示前に倍率を知るため `GetDpiForWindow` を使う）
- 今日の対象を、時刻 → `No` の順に表示する。未対応（未・スヌーズ）を上、完了を下の `Expander`（既定は閉じる・完了が 1 件以上のときだけ「完了 (件数)」）。0 件のときは「今日のリマインダーはありません。」
- レイアウトの寸法：タイトル帯の Padding 14,10,_,2・操作行の Margin 14,6,14,8（ボタンの Padding 10,4・アイコン 13 + 文字・間 6）・区切り線（`DividerStrokeColorDefaultBrush`）・本体の Margin 10,0,10,10・行は高さ 48・角丸 8・行間 6
- 状態の切り替えの各ボタンは Padding 10,4・文字 12・MinWidth 0（暗黙スタイル。`BasedOn` はライブラリのキーを参照できない恐れがあるので付けない）
- 行はカード
  - 状態の縦線（幅 6・角丸 3）。色は固定で、未 `#9E9E9E` / スヌーズ `#F59E0B` / 完了 `#16A34A`。完了の緑は、濃い緑（`#2E7D32` など）だとダークテーマで沈むため、明るいエメラルド系にしている
  - 時刻（15 SemiBold）
  - 件名（13 SemiBold）：SDK の `LinkArea`（`MmmSdk.WinUI.Controls`。Grid 派生）。リンクがあると手のカーソル + ホバー背景で、押すと開く。完了は取り消し線 + 不透明度 0.5
  - 状態の切り替え：CommunityToolkit の `Segmented`（NuGet `CommunityToolkit.WinUI.Controls.Segmented`）を `StatusIndex` と双方向でつなぐ。ユーザーが変えたときだけ `SetStateAsync` を呼び、読み直しの反映では保存しない
- 右クリック：リンクを開く（リンクのある行だけ有効）/ 編集 / 削除（論理削除・確認なし。一覧と同じ）
- 変更は差分更新する（`No` で同定・不足は挿入・余剰は削除・順番は `Move`。完了の欄の開閉を保つため）。行は `ReminderTodayItem`（読み直しでは `Apply` で中身だけを差し替える）
- 日付が変わったら新しい日に切り替える（読み直すたびに、次の 0 時 + 1 秒のタイマーを掛け直す）
- 通知との連携：通知の本文クリックの `onClicked` から、`DispatcherQueue`（Low）でいったん後回しにして `ShowFromNotificationAsync` を呼ぶ（閉じかけの通知ウィンドウと、別のウィンドウの操作が重ならないように）。開いてから `ReminderMonitor.SnoozeTriggeredAsync` を呼ぶ
- ダイアログの親：`IDialogHost.TrackWindow` で普通のウィンドウ（メインウィンドウ・この画面）を覚え、モーダルが無いときは最後にアクティブになったほうを親にする（この画面から開いた一覧・入力・確認が、この画面の上に出るように）

## 設定
スヌーズ間隔は設定ページ（[settings.md](settings.md)）で変える。
