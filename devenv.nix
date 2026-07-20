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

    # Stryker の MTP preview runner は solution 検出時に test-projects の固定を無視して全テストを掃く。
    # 道具側の欠陥境界として stryker 段だけ solution を退避する。
    mv TypeModeling.slnx TypeModeling.slnx.stryker-shadow
    trap 'mv TypeModeling.slnx.stryker-shadow TypeModeling.slnx' EXIT
    DOTNET_PROCESSOR_COUNT=1 dotnet stryker --skip-version-check
  '';
}
