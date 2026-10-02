{ pkgs, inputs, ... }:

let
  # 変異対象の project と、それを検査するテスト project の対。一行一対。
  # push の前は、テストの全件が T2 の予算に収まる対だけを変更した行で検査する
  pushMutationPairs = ''
    src/TypeModeling/TypeModeling.csproj tests/TypeModeling.Tests/TypeModeling.Tests.csproj
    src/TypeModeling.Analyzers/TypeModeling.Analyzers.csproj tests/TypeModeling.Analyzers.Tests/TypeModeling.Analyzers.Tests.csproj
  '';
  # 全量は全対を対象にする。TypeModeling.Testing.Tests は fixture ごとに MSBuild の子 process を直列に起動し、
  # 全件で 15 分を超えるので、この対の mutation は T3 だけで行う
  fullMutationPairs = pushMutationPairs + ''
    src/TypeModeling.Testing/TypeModeling.Testing.csproj tests/TypeModeling.Testing.Tests/TypeModeling.Testing.Tests.csproj
  '';
in
{
  packages = [
    pkgs.dotnet-sdk_10
    pkgs.just
    pkgs.git
    pkgs.jq
  ];

  env = {
    DOTNET_CLI_TELEMETRY_OPTOUT = "1";
    DOTNET_NOLOGO = "1";
  };

  # release タグの commit で固定した mutation-dotnet を、build が相対参照する typemodeling-dotnet と
  # 兄弟の directory へ写して一度だけ build し、引数をそのまま渡して実行する
  scripts.mutation-dotnet.exec = ''
    set -euo pipefail

    tool_root="$DEVENV_STATE/mutation-dotnet/${inputs.mutation-dotnet.rev}-${inputs.mutation-dotnet-upstream.rev}"
    if [ ! -f "$tool_root/built" ]; then
      rm -rf "$tool_root"
      mkdir -p "$tool_root"
      cp -R --no-preserve=mode ${inputs.mutation-dotnet} "$tool_root/mutation-dotnet"
      cp -R --no-preserve=mode ${inputs.mutation-dotnet-upstream} "$tool_root/typemodeling-dotnet"
      dotnet build "$tool_root/mutation-dotnet/src/Mutation.Cli/Mutation.Cli.csproj" --nologo --verbosity quiet >&2
      touch "$tool_root/built"
    fi
    exec dotnet "$tool_root/mutation-dotnet/src/Mutation.Cli/bin/Debug/net10.0/Mutation.Cli.dll" "$@"
  '';

  # T1(予算 2 分): build と analyzer の検査、Small test
  scripts.verify.exec = ''
    set -euo pipefail

    DOTNET_PROCESSOR_COUNT=1 dotnet build TypeModeling.slnx --no-restore

    for test_assembly in \
      tests/TypeModeling.Tests/bin/Debug/net10.0/TypeModeling.Tests.dll \
      tests/TypeModeling.Analyzers.Tests/bin/Debug/net10.0/TypeModeling.Analyzers.Tests.dll
    do
      dotnet "$test_assembly" --no-ansi --disable-logo --no-progress
    done
  '';

  # T2(予算 15 分): T1、出荷する検査の自己適用、push する範囲で変更した行の mutation
  scripts.verify-push.exec = ''
    set -euo pipefail

    verify
    # 自リポジトリへの検査の適用(ADR 0004)。fixture を組む残りの Medium test は予算を超えるので verify-full で実行する
    dotnet tests/TypeModeling.Testing.Tests/bin/Debug/net10.0/TypeModeling.Testing.Tests.dll \
      --no-ansi --disable-logo --no-progress \
      --treenode-filter '/*/TypeModeling.Testing.Tests/SelfAuditTests/*'
    mutation-changed-lines
  '';

  # push する範囲で変更した行の mutation。生き残った mutant を直した後は、これだけを再実行できる
  scripts.mutation-changed-lines.exec = ''
    set -euo pipefail

    # push する範囲の比較の基点。upstream が無い枝は統合ブランチ origin/develop から分かれた点にする。
    # 基点や差分を取れない実行は、変更した行が無い実行と区別できないので set -e で失敗にする
    if upstream=$(git rev-parse --abbrev-ref --symbolic-full-name '@{upstream}' 2>/dev/null); then
      base=$(git merge-base HEAD "$upstream")
    else
      base=$(git merge-base HEAD origin/develop)
    fi

    projects=()
    test_projects=()
    while read -r project test_project; do
      [ -n "$project" ] || continue
      changed=$(git diff --name-only --no-renames --diff-filter=d "$base" -- "$(dirname "$project")/*.cs")
      if [ -n "$changed" ]; then
        projects+=("$project")
        test_projects+=("$test_project")
      fi
    done <<<'${pushMutationPairs}'

    if [ "''${#projects[@]}" -eq 0 ]; then
      echo "mutation-changed-lines: $base から、push の前に変異させる project の .cs に変更が無いため mutation を省いた"
      exit 0
    fi

    output=.mutation-output/push
    report=$output/reports/mutation-report.json
    rm -f "$report"
    mutation-dotnet run \
      --project "$(IFS=,; echo "''${projects[*]}")" \
      --test-project "$(IFS=,; echo "''${test_projects[*]}")" \
      --since "$base" --changed-lines --output "$output" --with-baseline

    # --changed-lines は変更した行に重なる mutant だけを生成し、その生存と未被覆は changed-lines が数える。
    # 未検出があれば 2、報告か差分を読めなければ 1 で終わり、set -e で失敗にする
    mutation-dotnet changed-lines --report "$report" --since "$base"
  '';

  # T3(予算なし、gate にしない): T1、全件の Medium test、全量の mutation。
  # 前回の全量から増えた未検出 mutant を backlog へ追記する
  scripts.verify-full.exec = ''
    set -euo pipefail

    verify
    dotnet tests/TypeModeling.Testing.Tests/bin/Debug/net10.0/TypeModeling.Testing.Tests.dll \
      --no-ansi --disable-logo --no-progress

    projects=()
    test_projects=()
    while read -r project test_project; do
      [ -n "$project" ] || continue
      projects+=("$project")
      test_projects+=("$test_project")
    done <<<'${fullMutationPairs}'

    output=.mutation-output/full
    report=$output/reports/mutation-report.json
    previous=$output/previous-undetected.json
    current=$output/undetected.json
    if [ -f "$current" ]; then
      mv "$current" "$previous"
    fi
    rm -f "$report"
    mutation-dotnet run \
      --project "$(IFS=,; echo "''${projects[*]}")" \
      --test-project "$(IFS=,; echo "''${test_projects[*]}")" \
      --output "$output" --with-baseline

    # 変異の生成が 0 件の全量は対象の指定が外れている
    generated=$(jq '[.files[].mutants[]] | length' "$report")
    if [ "$generated" -eq 0 ]; then
      echo "verify-full: 変異の生成が 0 件のため失敗とする" >&2
      exit 1
    fi

    # 行番号のずれで同じ mutant を新規と数えないよう、file・演算子・置換・元の行の内容で照合する
    jq --arg top "$(git rev-parse --show-toplevel)/" '(.projectRoot | rtrimstr("/")) as $root
      | [.files | to_entries[]
          | ($root + "/" + .key | ltrimstr($top)) as $file
          | (.value.source | split("\n")) as $lines
          | .value.mutants[]
          | select(.status == "Survived" or .status == "NoCoverage")
          | {file: $file, line: .location.start.line, mutator: .mutatorName, replacement, status,
             code: ($lines[.location.start.line - 1] // "" | gsub("^\\s+|\\s+$"; ""))}]' "$report" >"$current"

    if [ -f "$previous" ]; then
      new=$(jq --slurpfile previous "$previous" '
        ($previous[0] | map({file, mutator, replacement, code})) as $known
        | map(select(({file, mutator, replacement, code}) as $key | any($known[]; . == $key) | not))' "$current")
    else
      new=$(cat "$current")
    fi
    echo "verify-full: 生成 $generated 件、未検出 $(jq length "$current") 件、前回の全量から増えた未検出 $(jq length <<<"$new") 件"

    if [ "$(jq length <<<"$new")" -gt 0 ]; then
      backlog=docs/backlog/mutation.md
      mkdir -p "$(dirname "$backlog")"
      {
        printf '\n## %s の verify-full(HEAD %s)\n\n' "$(date -Iseconds)" "$(git rev-parse --short HEAD)"
        jq --raw-output '.[] | "- [ ] \(.status) `\(.file):\(.line)` \(.mutator) → `\(.replacement)`"' <<<"$new"
      } >>"$backlog"
      echo "verify-full: 増えた未検出 mutant を $backlog へ追記した"
    fi
  '';
}
