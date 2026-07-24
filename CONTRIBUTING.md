# CONTRIBUTING

## ブランチ

統合ブランチは `develop`、リリースブランチは `main`。変更は `develop` から枝を切り、作業後に `develop` へ戻す。`main` への直接コミットはしない。

ブランチ名は用途を表す prefix を完全形で付ける(`feat/` ではなく `feature/`)。

| prefix      | 用途                         |
| ----------- | ---------------------------- |
| `feature/`  | 機能追加                     |
| `fix/`      | バグ修正                     |
| `refactor/` | 挙動を変えない構造改善       |
| `chore/`    | 雑務・依存更新・リリース準備 |
| `docs/`     | ドキュメント                 |

## コミット

`型: 要約` の一行で書き、型はブランチの prefix に揃える。要約は変更内容が読み取れる日本語にし、本文は付けない。一つのコミットには一つの関心のみを含め、無関係な変更は分ける。`Co-authored-by` などの自動生成痕跡は残さない(`commit-msg` フックが拒否する)。

## マージの条件

`develop` へ戻す前に `devenv shell verify` を通す。全 project の build が警告0、全テストが緑、mutation が break を超えていることが条件になる。
規範は architecture-standard に従う。設計のメモと裁定の記録は、git 管理外の docs/ に置く。

## マージ

`develop` へは `--no-ff` でマージし、`Merge branch '<branch>' into develop` のマージコミットを残す。作業ブランチはマージ後に削除する。

## リリース

1. `develop` で `CHANGELOG.md` に該当バージョンの節を追記(セマンティックバージョニング)
2. `chore: release X.Y.Z` でコミットし `develop` へマージ
3. `main` を該当コミットへ進め、`vX.Y.Z` タグを付ける

registry への配布は行わない。消費側は checkout したリポジトリの `main` のタグを相対参照で取り込む。
