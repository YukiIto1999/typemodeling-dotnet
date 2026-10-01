# CONTRIBUTING

## 開発環境

devenv が .NET SDK と検証入口を管理する。作業前にリポジトリの root で `devenv shell` を実行する。

## ブランチ

統合ブランチは `develop`、リリースブランチは `main`。変更は `develop` から枝を切り、作業後に `develop` へ戻す。`main` への直接コミットはしない。

ブランチ名は commit の型と同じ prefix を付ける。

| prefix      | 用途                         |
| ----------- | ---------------------------- |
| `feat/`     | 機能追加                     |
| `fix/`      | バグ修正                     |
| `refactor/` | 挙動を変えない構造改善       |
| `docs/`     | ドキュメント                 |
| `test/`     | テスト                       |
| `style/`    | 挙動に影響しない表記の統一   |
| `chore/`    | 雑務・依存更新・リリース準備 |
| `ci/`       | CI 設定                      |

## コミット

`型: 要約` の一行で書き、型はブランチの prefix に揃える。要約は変更の目的を日本語の体言止めで書き、本文は付けない。要約の書き方は、[README](./README.ja.md#アーキテクチャの標準) が宣言する architecture-standard の `principles/documentation/commit-purpose.md` と `principles/documentation/sentence-endings.md` に従う。一つのコミットには一つの関心のみを含め、無関係な変更は分ける。`Co-authored-by` などの自動生成痕跡は残さない(`commit-msg` フックが拒否する)。

## マージ

フックはマージコミットにも一行の `型: 要約` を求めるため、`develop` へは `--no-ff` でマージし、マージコミットは `chore: <作業名>の枝を統合` と書く。作業ブランチはマージ後に削除する。

## 検証

検証は時間の予算で分けた三つの入口で行い、どれもリポジトリの root で `devenv shell <入口>` として実行する。所要時間が予算を超えた入口は失敗として扱い、その検証をより安い手段へ置き換えるか後の段へ移す。

| 入口          | 段 | 予算  | 実行する時点 | 中身                                                                                                                                         |
| ------------- | -- | ----- | ------------ | -------------------------------------------------------------------------------------------------------------------------------------------- |
| `verify`      | T1 | 2 分  | commit の前  | analyzer の警告をエラー扱いにした build(S3776 の認知的複雑度 15 を含む)、TypeModeling.Tests、TypeModeling.Analyzers.Tests                 |
| `verify-push` | T2 | 15 分 | push の前    | `verify`、SelfAuditTests、TypeModeling と TypeModeling.Analyzers の push の基点から変更した行の mutation(未検出 0 件)                      |
| `verify-full` | T3 | なし  | release の前 | `verify`、TypeModeling.Testing.Tests の全件、三つの source project 全量の mutation。gate にせず、前回の全量から増えた未検出 mutant を backlog へ追記する |

push の基点は、upstream を持つ枝では upstream との分岐点、持たない枝では `origin/develop` との分岐点とする。基点や差分を取れない実行は失敗とする。
変更した行の未検出の mutant は、mutation-dotnet の `changed-lines` command が報告の生存と未被覆の合計で数える。生き残った mutant を直した後は、`devenv shell mutation-changed-lines` で変更した行の mutation だけを再実行できる。

S3776 の既存違反は、git 管理外の基線台帳 `docs/conformance-baseline.json` に記録した member だけを、台帳の id を Justification に書いた `SuppressMessage` で抑止する。台帳に無い member へ抑止を加えない。違反を直したら、その `SuppressMessage` と台帳の行を同じ commit で消す。

マージ前に `verify` と `verify-push` を通す。

## 文書

公開する文書は README(英語)・README.ja(日本語)・CHANGELOG(英語)・CONTRIBUTING・LICENSE に限る。設計メモ、決定の記録(`docs/decisions/`)、基線台帳(`docs/conformance-baseline.json`)、mutation の backlog(`docs/backlog/`)は git 管理外の `docs/` に置く。公開文書の日本語は体言止めを基調にする。

## リリース

1. `develop` で `devenv shell verify-full` を実行し、`docs/backlog/mutation.md` へ追記された未検出 mutant をテストまたは仕様の修正候補として確かめる
2. `CHANGELOG.md` に該当バージョンの節を追記(セマンティックバージョニング)
3. `chore: X.Y.Z のリリースを準備` でコミットし `develop` へマージ
4. `main` を該当コミットへ進め、`vX.Y.Z` タグを付ける

registry への配布は行わない。利用側は checkout したリポジトリのリリースタグを参照する。
