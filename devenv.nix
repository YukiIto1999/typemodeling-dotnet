{ pkgs, ... }:

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

    # mutation testing は隣接 checkout の mutation-dotnet をゲートに使う
    dotnet build ../mutation-dotnet/Mutation.slnx --nologo --verbosity quiet
    dotnet ../mutation-dotnet/src/Mutation.Cli/bin/Debug/net10.0/Mutation.Cli.dll run \
      --project src/TypeModeling/TypeModeling.csproj \
      --test-project tests/TypeModeling.Tests/TypeModeling.Tests.csproj \
      --output .mutation-output --with-baseline --break-at 60
  '';
}
