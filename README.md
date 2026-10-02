# MmmTool

## 取得方法

共有部品 [MmmSdk](https://github.com/Mimumumimu/MmmSdk) を Git サブモジュール（`external/MmmSdk`）として取り込んでいます。clone するときはサブモジュールも一緒に取得してください。

```
git clone --recurse-submodules https://github.com/Mimumumimu/MmmTool.git
```

すでに clone 済みで `external/MmmSdk` が空のときは、次を実行します。

```
git submodule update --init --recursive
```

## MmmSdk を更新するとき

SDK を直したら、先にサブモジュールの中（`master` ブランチ）でコミット・push し、そのあと MmmTool 側で「新しいコミットを指す」変更をコミットします。

```
cd external/MmmSdk
git switch master
# …変更をコミットして push…
cd ../..
git add external/MmmSdk
git commit
```

SDK の最新を取り込むときは、次を実行します。

```
git submodule update --remote external/MmmSdk
```
