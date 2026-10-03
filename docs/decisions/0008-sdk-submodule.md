# 0008 共有部品を別リポジトリの SDK に分け、サブモジュールで取り込む

## 状況
JSON の保存・設定ストア・ウィンドウ位置の保存・パスを開く処理・通知ダイアログは、このアプリ以外でも使える汎用の部品。

## 決定
- 別リポジトリ `MmmSdk`（`MmmSdk.Core` / `MmmSdk.WinUI`）に分ける
- アプリは Git サブモジュール `external/MmmSdk` として取り込み、プロジェクト参照でつなぐ
- アプリ固有の Entity・Repository は、アプリの Core に残す
- SDK を直したら、サブモジュールの中（master）でコミット・push してから、アプリ側で「新しいコミットを指す」コミットをする（SDK が先）

## 理由
複数のアプリで同じ部品を使い回せる。SDK はアプリを知らない（参照の向きは一方向）。

## 影響
clone は `--recurse-submodules`（取りこぼしたら `git submodule update --init --recursive`）。共通のパッケージ（Windows App SDK など）のバージョンは、各リポジトリに 1 か所ずつ書くので、上げるときは SDK を先にする。
