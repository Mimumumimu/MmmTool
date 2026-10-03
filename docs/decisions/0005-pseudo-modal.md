# 0005 モーダルは擬似モーダルで実現する

## 状況
リマインダーの一覧・入力画面は、開いている間、親のウィンドウを操作させたくない。WinUI 3 のウィンドウには、標準のモーダル表示がない。

## 決定
SDK の `PseudoModal`（`MmmSdk.WinUI.Components.Windowing`）で実現する。

- `SetOwner`（`GWLP_HWNDPARENT`）で親を設定する
- 表示したら `EnableWindow(親, false)` で親を操作不可にする
- 閉じる前に親を操作可能に戻し、前面に出す
- 親の中央に出し、作業領域からはみ出す分は内側へ寄せる

`OverlappedPresenter.IsModal = true` は使わない。

## 理由
`IsModal = true` を試したが、親を操作できてしまった。モーダルらしく見えて動けば、やり方は問わない。

## 注意
自分が消える前に親を戻さないと、別のアプリが前面に来る。コードから閉じるときは `PseudoModal.Close()` を `Close()` の前に呼ぶ。× / Alt+F4 は `AppWindow.Closing`、念のため `Closed` でも戻す。
