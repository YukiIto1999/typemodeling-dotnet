{ pkgs, inputs, ... }:

{
  packages = [
    pkgs.dotnet-sdk_10
    pkgs.just
    pkgs.git
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

  scripts.verify.exec = ''
    set -euo pipefail

    DOTNET_PROCESSOR_COUNT=1 dotnet build TypeModeling.slnx --no-restore

    for test_assembly in \
      tests/TypeModeling.Tests/bin/Debug/net10.0/TypeModeling.Tests.dll \
      tests/TypeModeling.Analyzers.Tests/bin/Debug/net10.0/TypeModeling.Analyzers.Tests.dll \
      tests/TypeModeling.Testing.Tests/bin/Debug/net10.0/TypeModeling.Testing.Tests.dll
    do
      dotnet "$test_assembly" --no-ansi --disable-logo --no-progress
    done

    mutation-dotnet run \
      --project src/TypeModeling/TypeModeling.csproj \
      --test-project tests/TypeModeling.Tests/TypeModeling.Tests.csproj \
      --output .mutation-output --with-baseline --break-at 60
  '';
}
