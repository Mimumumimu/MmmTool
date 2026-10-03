# 0003 フレームワーク依存で配布する

## 状況
.NET と Windows App SDK のランタイムを同梱する（自己完結）と、出力のファイル数と容量が大きくなる。

## 決定
- `SelfContained=false` / `WindowsAppSDKSelfContained=false` のフレームワーク依存にする。実行する PC に .NET 10 Desktop Runtime と Windows App Runtime 2.5 が必要
- `PublishTrimmed` は使わない（トリミングは自己完結でないと使えないため）。ただし、将来トリミングへ戻しても動くよう、JSON はソース生成のままにする
- 単一ファイル化（`PublishSingleFile`）は見送る
- WinUI の多言語リソース（言語名フォルダ内の `.mui`）は、`PruneMuiAfterBuild` / `PruneMuiAfterPublish` で ja-JP・en-us 以外を削除する

## 理由
配布物をできるだけ小さくするため。ランタイムの導入は、配布先でインストールしてもらう。

## 見直す条件
ランタイムを入れてもらえない配布先ができたときは、自己完結に切り替える。
