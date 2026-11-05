; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md
; TYPMOD002: ValueObject の Parse 欠落と署名不適合を生成時の error として報告

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|------
TYPMOD002 | TypeModeling | Error | ValueObject の Parse 欠落と引数型、返却型、アクセシビリティを含む署名不適合の禁止
TYPMOD003 | TypeModeling | Error | abstract でない閉じた class の禁止
TYPMOD004 | TypeModeling | Error | 別の assembly の閉じた class からの直接の派生の禁止(C# 15 の CS9382 に対応)
TYPMOD005 | TypeModeling | Error | 閉じた基底型で参照しない型引数の禁止(C# 15 の CS9383 に対応)
TYPMOD006 | TypeModeling | Error | 閉じた型の網羅で到達できない arm の禁止(C# 15 の CS8510 に対応)

### Removed Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|------
TYPMOD001 | TypeModeling | Warning | ClosedUnion、JsonPolymorphic、closed enum の variant を case pattern または when guard の is・constant・built-in equality/inequality で判別する switch statement の禁止。算術演算を含む式は対象外
