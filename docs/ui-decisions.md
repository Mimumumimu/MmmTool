# 画面の決まり (MmmTool 全体)

特定の機能の仕様に属さず、MmmTool の画面全体に効く、見た目と動きの決まり。  
機能ごとの決まりは、`specs/` の各機能のファイルにある。

## 文字とフォント

- トレイメニューの文字は、`BIZ UDGothic` で描く。無い環境では `Yu Gothic UI`、`Segoe UI` の順に使う。
  - 描き方は、[specs/tray-and-main.md](specs/tray-and-main.md) の「トレイアイコン・トレイメニュー」にある。
- 整形したテキストの等幅のフォントは、`BIZ UDGothic` で、無い環境では `MS Gothic` にする。
  - 作業報告のプレビューの例は、[specs/work-report.md](specs/work-report.md) にある。

## プレビューに足す文

- プレビュー (条件から自動で作り直す表示)に、手で足す文の入力欄を添えるときは、その欄の中身を保存しない。
