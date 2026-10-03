# 0012: SDK のフォルダを「Components / Controls / Utilities」の層に分ける

## 背景
SDK（MmmSdk）は「1 部品 = 1 フォルダ」で、プロジェクトの直下にフォルダが Core 13 個・WinUI 14 個並び、中身が 1〜2 ファイルのものが多かった。画面を持つ部品（通知・トレイ・ダイアログ・ターミナル）と、小さな道具（`FireAndForget`・`Debouncer`・`VisualTreeSearch`・`ImeControl`）が同じ段に並び、どれが何なのか見分けにくかった。同じテーマが 2 つのフォルダに分かれているものもあった（WinUI の `ConPty/` と `Terminal/`）。

## 決定
プロジェクトの直下に「Components / Controls / Utilities」の層を 1 段足す。プロジェクトは `MmmSdk.Core` / `MmmSdk.WinUI`（UI の有無で分ける）の 2 つのまま増やさない。名前空間はフォルダどおり。型の名前は変えない。動きも変えない。

## どの層に入れるかの基準
- **Components**（部品）: 何をするための部品かを名前で言えるもの（保存・設定・通知・トレイ・ターミナル など）。部品ごとにサブフォルダを作り、その部品の型（サービス・画面・データ・その部品専用の static や拡張メソッド・その部品の中で使うコントロール）を一式まとめて置く
- **Controls**（画面部品）: どの部品にも属さない、XAML に置いて使う汎用のコントロール
- **Utilities**（道具）: どの部品にも属さない、汎用の小さな道具（拡張メソッド・小さなヘルパー。static に限らない）。サブフォルダは作らず平置き
- 迷ったら「その型は、ある部品の一部か」で決める。一部なら、その部品のフォルダに置く（例: `ReadableJsonOptions` は Storage、`ScreenGeometry` は WindowPositions、`SettingsStoreExtensions` は Settings、`TerminalControl` は Terminal、`ThumbnailImage` は Attachments）
- `Interop/` は土台なので層の外。DI 登録の `Sdk*ServiceCollectionExtensions.cs` もプロジェクト直下
- Core と WinUI で同じテーマのフォルダは同じ名前にする（`Attachments`・`Notifications`）

## 主な移動
- Core: `Attachments` `Logging` `Notifications` `Paths` `Scheduling` `Settings` `Shells` `SingleInstance` `Storage` `WindowPositions` → `Components/`。`Tasks/*`・`Collections/*` → `Utilities/`
- WinUI: `Attachments` `Dialogs` `Errors` `Notifications` `Tray` `Terminal` → `Components/`。`ConPty/PseudoConsole` → `Components/Terminal/`（ConPty と Terminal を 1 つにまとめた）。`Dialogs/PseudoModal`・`Windowing/WindowBoundsKeeper` → `Components/Windowing/`。`Dialogs/NativeMessageBox`・`Windowing/WindowExtensions`・`Windowing/WindowPlacement`・`VisualTree/VisualTreeSearch`・`Input/ImeControl` → `Utilities/`
- ターミナルの資材は `Components/Terminal/Assets/`。出力先（`Assets/Terminal`）は csproj の `Link` で決まっていて、変えない

## 理由
- 1 部品 = 1 フォルダで、大きさの違うもの（画面を持つ部品と、小さな道具）が同じ段に並び、見分けにくかった。層で分ければ、「何をする部品か」と「道具」を別の段に置ける
- プロジェクトを細かく分けなかった理由: CsWin32 が作る型は internal なので、WinUI 側を複数のプロジェクトに分けると、各プロジェクトに CsWin32 を持たせる（宣言が散らばる）か、Win32 の型を公開する Interop プロジェクトを作るしかなく、[決定 0011](0011-cswin32.md)（Win32 の宣言は SDK の 1 か所）を崩す。プロジェクトを分ける意味がある「依存先の違い」「別パッケージでの配布」にも当たらない
- アプリ側（`Features/<機能>/`）に同じ層を作らない理由: アプリは「機能」で分け、SDK は「部品」で分けていて、分ける軸がもともと違う

## 最終形にどう近づくか
SDK の部品は、層と部品名の 2 段で探せる形に集まり、部品が増えても直下の見分けやすさが保たれる。
