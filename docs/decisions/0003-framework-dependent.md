# 0003 フレームワーク依存で配布する

## 状況
.NET と Windows App SDK のランタイムを同梱する（自己完結）と、出力のファイル数（DLL の数）と容量が大きくなる。

## 決定
- `SelfContained=false` / `WindowsAppSDKSelfContained=false` のフレームワーク依存にする。実行する PC に .NET 10 Desktop Runtime と Windows App Runtime 2.5 が必要
- `PublishTrimmed` は使わない（トリミングは自己完結でないと使えないため）。ただし、将来トリミングへ戻しても動くよう、JSON はソース生成のままにする
- 単一ファイル化（`PublishSingleFile`）はしない（1 つの EXE が 100 MB を超えるような、巨大な 1 ファイルにしたくないため）
- ReadyToRun（`PublishReadyToRun`。Release の発行のみ）は使う。DLL を増やさず、起動を速くするため（DLL の数は変わらない。サイズは多少増えるが、目的は「DLL の数を減らす」ことで、サイズを切り詰めることではない）
- WinUI の多言語リソース（言語名フォルダ内の `.mui`）は、`PruneMuiAfterBuild` / `PruneMuiAfterPublish` で ja-JP・en-us 以外を削除する

## 理由
**DLL の数をできるだけ少なくするため**（ランタイムを同梱すると、DLL が大量に増える）。かといって、1 つの EXE に全部まとめて巨大にもしたくない（単一ファイルにはしない）。ランタイムの導入は、配布先でインストールしてもらう。

「配布物を小さく」は、サイズを最小にする意味ではない。ランタイムを同梱しないことで、DLL の数と全体の容量が自然に小さくなる、という意味で使っている。サイズだけを理由に、ReadyToRun などの起動を速くする設定を外さない。

## 見直す条件
ランタイムを入れてもらえない配布先ができたときは、自己完結に切り替える。
