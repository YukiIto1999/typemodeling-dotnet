# CONTRIBUTING

この文書の読み手は、このリポジトリを変更する開発者である。

## 開発環境

devenv が .NET SDK と検証入口を管理する。
作業前に、リポジトリの root で `devenv shell` を実行する。

## ブランチ

統合ブランチは `develop`、リリースブランチは `main` である。
変更は `develop` から枝を切り、作業後に `develop` へ戻す。
`main` へは直接コミットしない。

ブランチ名には、commit の型と同じ prefix を付ける。

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

commit の件名は `型: 要約` の一行で書き、型はブランチの prefix に揃え、本文は付けない。
要約の書き方は、[README](./README.ja.md#アーキテクチャの標準) が宣言する architecture-standard の `principles/documentation/commit-purpose.md` と `principles/documentation/sentence-endings.md` に従う。
一つのコミットには一つの関心だけを含め、無関係な変更は分ける。
`Co-authored-by` のような自動生成の痕跡は残さず、`commit-msg` フックがそれを拒否する。

## マージ

フックはマージコミットにも一行の `型: 要約` を求めるので、`develop` へは `--no-ff` でマージし、マージコミットを `chore: <作業名>の枝の統合` と書く。
作業ブランチは、マージした後に削除する。

## 検証

検証は時間の予算で分けた三つの入口で行い、どれもリポジトリの root で `devenv shell <入口>` として実行する。
所要時間が予算を超えた入口は失敗として扱い、その検証をより安い手段へ置き換えるか後の段へ移す。
変更した C# file は、`devenv shell -- csharpier format --ignore-path .gitignore <files>` で整形する。
`<files>` には今回変更した file の path を渡し、無関係な file の整形を混ぜない。
formatter は `devenv.lock` が固定する Nixpkgs から供給し、個人の global install を前提にしない。
ignore file はこの repository のものを明示し、別 repository の無視対象 directory 内に置いた worktree まで祖先の設定で除外しない。

| 入口          | 段 | 予算  | 実行する時点 | 中身                                                                                                                                         |
| ------------- | -- | ----- | ------------ | -------------------------------------------------------------------------------------------------------------------------------------------- |
| `verify`      | T1 | 2 分  | commit の前  | analyzer の警告をエラー扱いにした build(S3776 の認知的複雑度 15 を含む)、TypeModeling.Tests、TypeModeling.Analyzers.Tests                 |
| `verify-push` | T2 | 15 分 | push の前    | `verify` と SelfAuditTests |
| `verify-full` | T3 | なし  | 分析が必要なとき | `verify`、TypeModeling.Testing.Tests の全件、三つの source project 全量の mutation と未検出 mutant の記録 |

時間予算は、root の `verification-budget.json` の `verify.seconds` と `verify-push.seconds` に宣言する。
push フックはこの予算で既存の `verify-push` を実行し、送信する commit と検証対象の一致を確認する。
push の前には、index・作業ツリー・未追跡 file に差分を残さない。

push の基点は、upstream を持つ枝では upstream との分岐点とし、持たない枝では `origin/develop` との分岐点とする。
基点や差分を取れない実行は、失敗とする。
mutation testing には、`devenv.yaml` で release タグの commit に固定して build した [mutation-dotnet](https://github.com/YukiIto1999/mutation-dotnet) を使う。
変更した行の mutation は、mutation-dotnet の `--changed-lines` で変更した行に重なる mutant だけを生成し、その未検出を `changed-lines` command が報告の生存と未被覆の合計で数える。
変更した行だけを分析するときは、`devenv shell mutation-changed-lines` を実行する。
分析の生存・未被覆・未分類・未実行や score は、commit、push、merge、release の承認条件にしない。
元の report と終了状態を保持し、未検出を製品の欠陥、test の不足、等価変異、許容する差分、別の property の責任範囲、runner の失敗に分類する。
修正した振る舞いを検証するために再実行し、未検出をゼロにするだけの再実行は行わない。

analyzer のテストは、実行環境の framework assembly の metadata 参照を同一 process 内で再利用する。
compilation と generator driver は検査ごとに作り、mutant の process 隔離と 2 worker の上限は維持する。

S3776 の既存違反を `SuppressMessage` で抑止する場合は、Justification に違反の複雑度と、15 以下へ分割した時点で抑止を外すことを書く。
新しい違反には抑止を加えず、15 以下へ分割する。

マージの前に、`verify` と `verify-push` を通す。

## 文書

公開する文書は、README(英語)、README.ja(日本語)、CHANGELOG(英語)、CONTRIBUTING、LICENSE である。
README は英語版を正本とし、日本語版を同じ変更で正本に合わせる。
日本語の文書は、architecture-standard の `principles/documentation/sentence-endings.md` に従い、段落と箇条書きを句点で終わる完結した文で書き、一つの文を一行に置く。
継続する読み手のための文書は `docs/` に置いて Git で追跡し、計画、調査、実験、下書き、mutation の backlog は Git の追跡対象から除外した `work/` に置く。
設計判断の理由は、`work/` だけに残さず、コード、テスト、commit、実装コメントから辿れるようにする。

## リリース

1. `develop` で `devenv shell verify-push` を通す。
   既存の mutation 分析に未解決の製品の欠陥がある場合は修正し、分析の未実行や未検出数を release の承認条件にしない。
2. `CHANGELOG.md` に、セマンティックバージョニングに従う該当バージョンの節を追記する。
3. `chore: X.Y.Z のリリース準備` でコミットし、`develop` へマージする。
4. `main` を該当コミットへ進め、`vX.Y.Z` タグを付ける。

registry へは配布せず、利用側は checkout したリポジトリのリリースタグを参照する。
